using TransitGate.Application.Accesos;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Notificaciones;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Validacion;

/// <summary>
/// Caso de uso de una lectura de tarjeta física (RF-18, RF-20 a RF-23). Comprueba, en este orden,
/// el torniquete, el soporte y el saldo (RF-20); si todo está bien, debita y abre un paso. Toda lectura
/// deja un IntentoAcceso, y el débito, el paso y el intento se guardan juntos (RNF-15).
/// </summary>
public sealed class ValidarLectura(
    ITorniqueteRepositorio torniquetes,
    IIntentoRepositorio intentos,
    ISoporteResolver soportes,
    ISaldoService saldos,
    IUnidadDeTrabajo unidadDeTrabajo,
    INotificador notificador,
    IParametrosOperacion parametros,
    CerrojoTorniquetes cerrojo,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<ResultadoLectura?> EjecutarAsync(
        string torniqueteId, string lecturaId, string uidHex, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        IntentoAcceso intento;
        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            // RF-18 CA4: un reintento con el mismo lecturaId recibe la misma respuesta y no cobra otra vez.
            if (await intentos.BuscarPorLecturaAsync(torniqueteId, lecturaId, ct) is { } previo)
                return ResultadoLectura.Desde(previo);

            var lectura = new Lectura(torniqueteId, lecturaId, uidHex, reloj.GetUtcNow().UtcDateTime);
            var antes = torniquete.Deseado;

            intento = await DecidirAsync(torniquete, lectura, ct);
            await intentos.AgregarAsync(intento, ct);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);

            bitacora.SiCambioElDeseado(torniquete, antes, "lectura");
            bitacora.Lectura(
                torniqueteId, lecturaId, uidHex, intento.Motivo?.ToString() ?? "abrir", lectura.Fecha);
        }

        // Fuera del cerrojo: avisar al panel no debe demorar la respuesta al ESP32 de la siguiente lectura.
        await notificador.IntentoRegistradoAsync(intento, ct);
        return ResultadoLectura.Desde(intento);
    }

    private async Task<IntentoAcceso> DecidirAsync(Torniquete torniquete, Lectura lectura, CancellationToken ct)
    {
        torniquete.ActualizarPorTiempo(lectura.Fecha);

        // Se identifica la tarjeta aunque el torniquete esté ocupado, para que el rechazo quede en su historial.
        var soporte = await soportes.PorUidAsync(lectura.Uid);
        var intentoId = await intentos.SiguienteIdAsync(ct);

        IntentoAcceso Rechazo(MotivoIntento motivo) => IntentoAcceso.Rechazado(
            intentoId, lectura.TorniqueteId, lectura.LecturaId, lectura.Fecha,
            TipoSoporte.Fisico, lectura.Uid, soporte?.TarjetaId, motivo);

        if (torniquete.MotivoNoDisponible is { } motivoTorniquete)
            return Rechazo(motivoTorniquete);
        if (soporte is null)
            return Rechazo(MotivoIntento.TarjetaNoRegistrada);
        if (soporte.Estado == EstadoSoporte.Bloqueada)
            return Rechazo(MotivoIntento.SoporteBloqueado);
        if (soporte.Estado != EstadoSoporte.Activa)
            return Rechazo(MotivoIntento.SoporteNoActivo);

        var tarifa = await saldos.TarifaVigenteAsync();
        var pasoId = await torniquetes.SiguientePasoIdAsync(ct);
        var debito = await saldos.DebitarAsync(soporte.TarjetaId, tarifa, pasoId);
        if (!debito.Exitoso)
            return Rechazo(MotivoIntento.SaldoInsuficiente);

        var paso = torniquete.AutorizarPaso(pasoId, lectura.Fecha, parametros.VentanaPaso, parametros.GraciaVision);
        return IntentoAcceso.Exitoso(
            intentoId, lectura.TorniqueteId, lectura.LecturaId, lectura.Fecha,
            TipoSoporte.Fisico, lectura.Uid, soporte.TarjetaId, tarifa, paso.Id, debito.SaldoRestante);
    }

    private sealed record Lectura(string TorniqueteId, string LecturaId, string Uid, DateTime Fecha);
}
