using TransitGate.Application.Notificaciones;

namespace TransitGate.Api.Contratos;

// Eventos de SignalR del hub /hubs/eventos (contratos de integración §6).

/// <summary>
/// estadoDesde y ultimaSenal son UTC y pueden ser null: el primero si el torniquete no ha cambiado de estado
/// desde que arrancó la API, el segundo si el ESP32 todavía no ha enviado ningún latido.
/// </summary>
public record EstadoTorniqueteEvento(
    string TorniqueteId,
    string Estado,
    DateTime? EstadoDesde,
    bool Esp32Conectado,
    DateTime? UltimaSenal,
    bool VisionConectada,
    bool Desincronizado)
{
    public static EstadoTorniqueteEvento Desde(EstadoTorniqueteVista vista) => new(
        vista.TorniqueteId,
        vista.Estado.ToString(),
        vista.EstadoDesde,
        vista.Esp32Conectado,
        vista.UltimaSenal,
        vista.VisionConectada,
        vista.Desincronizado);
}
