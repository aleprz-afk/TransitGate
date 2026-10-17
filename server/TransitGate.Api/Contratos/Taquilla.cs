using TransitGate.Application.Cuentas;

namespace TransitGate.Api.Contratos;

// JSON del Contrato API: §4 (modo registro) y §8 (solicitudes de tarjeta física, RF-36).

public record SolicitudModoRegistro(string? SolicitudId);

/// <summary>expiraEn: segundos que dura el modo registro si no llega ninguna lectura (S6).</summary>
public record RespuestaModoRegistro(int ExpiraEn);

/// <summary>
/// Una solicitud pendiente en la lista de la taquilla (RF-36 CA1: nombre, documento y fecha). El documento va
/// con solo los 4 últimos dígitos, salvo en la búsqueda por documento, donde el operador ya lo conoce (RNF-27).
/// </summary>
public record SolicitudResumen(string SolicitudId, string ClienteId, string NombreCompleto, string Documento, DateTime Fecha)
{
    public static SolicitudResumen Desde(SolicitudPendiente solicitud, bool documentoCompleto) => new(
        IdsPublicos.Solicitud(solicitud.SoporteId),
        IdsPublicos.Cliente(solicitud.ClienteId),
        solicitud.NombreCompleto,
        documentoCompleto ? solicitud.Documento : Enmascarar(solicitud.Documento),
        solicitud.Fecha);

    private static string Enmascarar(string documento) =>
        documento.Length <= 4 ? documento : new string('•', documento.Length - 4) + documento[^4..];
}
