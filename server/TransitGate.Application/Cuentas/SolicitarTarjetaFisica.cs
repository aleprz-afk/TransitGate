using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

public sealed record SolicitudCreada(Guid SolicitudId, EstadoSoporte Estado);

/// <summary>
/// El cliente pide la tarjeta física desde el portal (RF-08): queda un soporte físico Solicitada, sin UID, que
/// aparece en la lista de la taquilla. Con una física Solicitada, Entregada o Activa no se puede pedir otra
/// (CA2); si la anterior está Bloqueada, es una reposición y el saldo no cambia (CA3).
/// </summary>
public sealed class SolicitarTarjetaFisica(
    ITarjetaRepositorio tarjetas,
    ISoporteRepositorio soportes,
    ISolicitudesTarjeta solicitudes,
    IUnidadDeTrabajo unidadDeTrabajo,
    INotificador notificador,
    TimeProvider reloj)
{
    public async Task<Resultado<SolicitudCreada>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        if (await tarjetas.DelClienteAsync(clienteId, ct) is not { } tarjeta)
            return ErrorCuenta.TarjetaNoEncontrada;

        var existentes = await soportes.DeLaTarjetaAsync(tarjeta.Id, ct);
        if (existentes.Any(s => s.Tipo == TipoSoporte.Fisico && s.Vigente))
            return ErrorCuenta.Conflicto("TarjetaFisicaExistente", "Ya tienes una tarjeta física solicitada o activa.");

        var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), tarjeta.Id, reloj.GetUtcNow().UtcDateTime);
        await soportes.AgregarAsync(soporte, ct);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);

        if (await solicitudes.PendientePorIdAsync(soporte.Id) is { } pendiente)
            await notificador.SolicitudNuevaAsync(pendiente, ct);

        return new SolicitudCreada(soporte.Id, soporte.Estado);
    }
}
