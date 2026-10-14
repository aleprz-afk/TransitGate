using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Validacion;

/// <summary>
/// Caso de uso de una lectura de tarjeta física (RF-18, RF-20, RF-21, RF-22). Comprueba, en este orden,
/// el torniquete, el soporte y el saldo (RF-20); si todo está bien, debita y abre un paso.
/// Pendiente para la semana 3: registrar el IntentoAcceso y guardar todo en una sola transacción (RNF-15).
/// </summary>
public sealed class ValidarLectura(
    ITorniqueteRepositorio torniquetes,
    ILecturasProcesadas lecturas,
    ISoporteResolver soportes,
    ISaldoService saldos,
    IParametrosOperacion parametros,
    CerrojoTorniquetes cerrojo,
    TimeProvider reloj)
{
    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<ResultadoLectura?> EjecutarAsync(
        string torniqueteId, string lecturaId, string uidHex, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            if (await lecturas.BuscarAsync(torniqueteId, lecturaId, ct) is { } previa)
                return previa;

            var resultado = await DecidirAsync(torniquete, uidHex, ct);
            await lecturas.GuardarAsync(torniqueteId, lecturaId, resultado, ct);
            return resultado;
        }
    }

    private async Task<ResultadoLectura> DecidirAsync(Torniquete torniquete, string uidHex, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        torniquete.ActualizarPorTiempo(ahora);

        if (torniquete.MotivoNoDisponible is { } motivoTorniquete)
            return ResultadoLectura.Denegar(motivoTorniquete);

        var soporte = await soportes.PorUidAsync(uidHex);
        if (soporte is null)
            return ResultadoLectura.Denegar(MotivoIntento.TarjetaNoRegistrada);
        if (soporte.Estado == EstadoSoporte.Bloqueada)
            return ResultadoLectura.Denegar(MotivoIntento.SoporteBloqueado);
        if (soporte.Estado != EstadoSoporte.Activa)
            return ResultadoLectura.Denegar(MotivoIntento.SoporteNoActivo);

        var tarifa = await saldos.TarifaVigenteAsync();
        var pasoId = await torniquetes.SiguientePasoIdAsync(ct);
        var debito = await saldos.DebitarAsync(soporte.TarjetaId, tarifa, pasoId);
        if (!debito.Exitoso)
            return ResultadoLectura.Denegar(MotivoIntento.SaldoInsuficiente);

        var paso = torniquete.AutorizarPaso(pasoId, ahora, parametros.VentanaPaso, parametros.GraciaVision);
        return ResultadoLectura.Abrir(paso.Id, debito.SaldoRestante);
    }
}
