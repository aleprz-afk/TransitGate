using TransitGate.Application.Notificaciones;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Alertas;

/// <summary>
/// El personal apaga la alarma desde el panel (RF-34): el torniquete vuelve a Cerrado y la alerta queda
/// Atendida. En el MVP no hace falta clasificarla después (RF-35 va en la entrega final).
/// </summary>
public sealed class ApagarAlarma(
    IAlertaRepositorio alertas,
    ITorniqueteRepositorio torniquetes,
    IUnidadDeTrabajo unidadDeTrabajo,
    INotificador notificador,
    CerrojoTorniquetes cerrojo,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    /// <summary>Devuelve false si la alerta no existe. Apagar una alarma ya apagada no hace nada.</summary>
    public async Task<bool> EjecutarAsync(long alertaId, CancellationToken ct = default)
    {
        if (await alertas.ObtenerAsync(alertaId, ct) is not { } alerta)
            return false;

        using (await cerrojo.TomarAsync(alerta.TorniqueteId, ct))
        {
            if (alerta.Estado != EstadoAlerta.Abierta)
                return true;

            var ahora = reloj.GetUtcNow().UtcDateTime;
            if (await torniquetes.ObtenerAsync(alerta.TorniqueteId, ct) is { Estado: EstadoTorniquete.Alarma } torniquete)
            {
                var antes = torniquete.Deseado;
                torniquete.ApagarAlarma(ahora);
                bitacora.SiCambioElDeseado(torniquete, antes, "apagar-alarma");
            }

            alerta.Atender(ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        }

        await notificador.AlertaActualizadaAsync(alerta, ct);
        return true;
    }
}
