using TransitGate.Application.Cuentas;
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

/// <summary>cliente va abreviado ("Luisa R."): el panel no muestra más de lo necesario (RNF-27).</summary>
public record SolicitudNuevaEvento(string SolicitudId, string Cliente, DateTime Fecha)
{
    public static SolicitudNuevaEvento Desde(SolicitudPendiente solicitud) => new(
        IdsPublicos.Solicitud(solicitud.SoporteId), Abreviar(solicitud.NombreCompleto), solicitud.Fecha);

    private static string Abreviar(string nombre)
    {
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length < 2 ? nombre : $"{partes[0]} {partes[1][0]}.";
    }
}

/// <summary>Al grupo del cliente. causa: Paso, Recarga o Reversion.</summary>
public record SaldoActualizadoEvento(int Saldo, string Causa, DateTime Fecha);

/// <summary>Al grupo del cliente: un intento con su tarjeta (RF-39 CA4). motivo es null en un exitoso.</summary>
public record EventoViajeEvento(long IntentoId, string Resultado, string? Motivo, string Torniquete, int? Tarifa, DateTime Fecha)
{
    public static EventoViajeEvento Desde(IntentoAcceso intento, string torniqueteNombre) => new(
        intento.Id, intento.Resultado.ToString(), intento.Motivo?.ToString(), torniqueteNombre,
        intento.TarifaCobrada is { } tarifa ? (int)tarifa : null, intento.Fecha);
}
