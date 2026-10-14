using TransitGate.Application.Notificaciones;

namespace TransitGate.Api.Contratos;

// Eventos de SignalR del hub /hubs/eventos (contratos de integración §6).

public record EstadoTorniqueteEvento(
    string TorniqueteId, string Estado, bool Esp32Conectado, bool VisionConectada, bool Desincronizado)
{
    public static EstadoTorniqueteEvento Desde(EstadoTorniqueteVista vista) => new(
        vista.TorniqueteId, vista.Estado.ToString(), vista.Esp32Conectado, vista.VisionConectada, vista.Desincronizado);
}
