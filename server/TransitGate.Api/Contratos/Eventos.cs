using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;

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

/// <summary>motivo es null en un intento exitoso. Sin datos del cliente ni UID (RNF-27).</summary>
public record IntentoRegistradoEvento(long IntentoId, string TorniqueteId, string Resultado, string? Motivo, DateTime Fecha)
{
    public static IntentoRegistradoEvento Desde(IntentoAcceso intento) => new(
        intento.Id, intento.TorniqueteId, intento.Resultado.ToString(), intento.Motivo?.ToString(), intento.Fecha);
}

public record AlertaNuevaEvento(string AlertaId, string TorniqueteId, string Tipo, DateTime Fecha)
{
    public static AlertaNuevaEvento Desde(AlertaSeguridad alerta) => new(
        IdsPublicos.Alerta(alerta.Id), alerta.TorniqueteId, alerta.Tipo.ToString(), alerta.Fecha);
}

public record AlertaActualizadaEvento(string AlertaId, string Estado)
{
    public static AlertaActualizadaEvento Desde(AlertaSeguridad alerta) => new(
        IdsPublicos.Alerta(alerta.Id), alerta.Estado.ToString());
}

/// <summary>resultado: Ligado, UidYaRegistrado o SolicitudNoPendiente. Sin el UID (RNF-27).</summary>
public record RegistroTarjetaEvento(string TorniqueteId, string SolicitudId, string Resultado, DateTime Fecha);
