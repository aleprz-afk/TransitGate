using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

/// <summary>
/// Recarga con pago simulado (RF-15). El monto debe estar entre 1.000 y 200.000 (S8, CA3). La recarga se
/// aplica aunque el soporte esté bloqueado o no exista, porque el saldo es de la tarjeta (CA4). Con
/// <c>simularRechazo</c> el pago simulado se rechaza: queda la Recarga rechazada y el saldo no cambia (CA2).
/// La misma clave de idempotencia aplica la recarga una sola vez (CA5).
/// </summary>
public sealed class Recargar(
    ITarjetaRepositorio tarjetas,
    IRecargaRepositorio recargas,
    IParametrosCuentas parametros,
    IUnidadDeTrabajo unidadDeTrabajo,
    INotificador notificador,
    CerrojoTarjetas cerrojo,
    TimeProvider reloj)
{
    public async Task<Resultado<Recarga>> EjecutarAsync(
        Guid clienteId, decimal monto, string? claveIdempotencia, bool simularRechazo, CancellationToken ct = default)
    {
        if (monto < parametros.MontoMinRecarga || monto > parametros.MontoMaxRecarga)
            return ErrorCuenta.Invalido(
                "MontoFueraDeRango",
                $"El monto debe estar entre {parametros.MontoMinRecarga:N0} y {parametros.MontoMaxRecarga:N0} COP.");
        if (await tarjetas.DelClienteAsync(clienteId, ct) is not { } suTarjeta)
            return ErrorCuenta.TarjetaNoEncontrada;

        var clave = string.IsNullOrWhiteSpace(claveIdempotencia) ? null : claveIdempotencia.Trim();
        Recarga recarga;
        Tarjeta tarjeta;
        using (await cerrojo.TomarAsync(suTarjeta.Id, ct))
        {
            tarjeta = (await tarjetas.ObtenerAsync(suTarjeta.Id, ct))!;
            if (clave is not null && await recargas.PorClaveAsync(tarjeta.Id, clave, ct) is { } previa)
                return previa;

            if (!simularRechazo)
                tarjeta.Acreditar(monto);

            recarga = new Recarga(
                await recargas.SiguienteIdAsync(ct), tarjeta.Id, monto, aprobada: !simularRechazo,
                tarjeta.Saldo, reloj.GetUtcNow().UtcDateTime, clave);
            await recargas.AgregarAsync(recarga, ct);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        }

        if (recarga.Aprobada)
            await notificador.SaldoActualizadoAsync(clienteId, tarjeta.Saldo, CausaSaldo.Recarga, ct);

        return recarga;
    }
}
