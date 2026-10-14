using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Notificaciones;

/// <summary>Contenido del evento EstadoTorniquete del grupo "operacion" (contratos de integración §6).</summary>
public sealed record EstadoTorniqueteVista(
    string TorniqueteId,
    EstadoTorniquete Estado,
    DateTime? EstadoDesde,
    bool Esp32Conectado,
    DateTime? UltimaSenal,
    bool VisionConectada,
    bool Desincronizado);

/// <summary>
/// Eventos en tiempo real hacia la web (contrato C7; lo implementa Alex sobre SignalR).
/// A diferencia del contrato, EstadoTorniqueteAsync recibe el estado ya calculado en lugar del id:
/// así no hay que volver a tomar el cerrojo del torniquete para armar el evento.
/// Los demás eventos de §6 se agregan a medida que existan.
/// </summary>
public interface INotificador
{
    Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado, CancellationToken ct = default);
}
