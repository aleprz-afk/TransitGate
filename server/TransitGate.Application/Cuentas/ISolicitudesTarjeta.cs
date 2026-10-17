namespace TransitGate.Application.Cuentas;

// Contrato C7 (contratos de integración §7, v1.6): entrega de la tarjeta física en taquilla (RF-09, RF-10, RF-36).
// Una solicitud es el SoporteTarjeta físico en estado Solicitada (RF-08 CA1); su id es el del soporte.

public record SolicitudPendiente(Guid SoporteId, Guid ClienteId, string NombreCompleto, string Documento, DateTime Fecha);

public enum ResultadoLigar
{
    /// <summary>El soporte pasó de Solicitada a Entregada con el UID leído (RF-10 CA1).</summary>
    Ligado,

    /// <summary>El UID ya pertenece a otro soporte, de cualquier cliente (RF-10 CA2). No cambia nada.</summary>
    UidYaRegistrado,

    /// <summary>La solicitud ya no está en Solicitada (por ejemplo, se ligó en otra taquilla).</summary>
    SolicitudNoPendiente,
}

public interface ISolicitudesTarjeta
{
    Task<SolicitudPendiente?> PendientePorIdAsync(Guid soporteId);

    /// <summary>Las solicitudes en Solicitada, la más antigua primero (RF-36 CA1); con documento, solo la de ese cliente (CA2).</summary>
    Task<IReadOnlyList<SolicitudPendiente>> PendientesAsync(string? documento);

    /// <summary>Participa en la transacción del caso de uso, como DebitarAsync: no guarda por su cuenta.</summary>
    Task<ResultadoLigar> LigarUidAsync(Guid soporteId, string uidHex);
}
