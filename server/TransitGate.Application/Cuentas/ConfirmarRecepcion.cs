using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

/// <summary>
/// El cliente confirma en el portal que recibió la tarjeta física (RF-11, S5): pasa de Entregada a Activa y
/// ese UID ya valida pasos. En otro estado no se permite (CA3). El reemplazo del celular (CA2) llega con la
/// Fase 4, cuando exista el soporte virtual.
/// </summary>
public sealed class ConfirmarRecepcion(
    ITarjetaRepositorio tarjetas,
    ISoporteRepositorio soportes,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    public async Task<Resultado<EstadoSoporte>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        if (await tarjetas.DelClienteAsync(clienteId, ct) is not { } tarjeta)
            return ErrorCuenta.TarjetaNoEncontrada;

        var entregada = (await soportes.DeLaTarjetaAsync(tarjeta.Id, ct))
            .FirstOrDefault(s => s.Tipo == TipoSoporte.Fisico && s.Estado == EstadoSoporte.Entregada);
        if (entregada is null)
            return ErrorCuenta.Conflicto("EstadoInvalido", "La tarjeta no está en estado Entregada.");

        entregada.Activar(reloj.GetUtcNow().UtcDateTime);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return entregada.Estado;
    }
}
