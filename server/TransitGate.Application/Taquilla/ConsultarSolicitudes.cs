using TransitGate.Application.Cuentas;

namespace TransitGate.Application.Taquilla;

/// <summary>
/// Lista de solicitudes de tarjeta física pendientes para la taquilla (RF-36): la más antigua primero,
/// o solo la del documento buscado. Una solicitud ya ligada no aparece (CA4).
/// </summary>
public sealed class ConsultarSolicitudes(ISolicitudesTarjeta solicitudes)
{
    public Task<IReadOnlyList<SolicitudPendiente>> EjecutarAsync(string? documento) =>
        solicitudes.PendientesAsync(string.IsNullOrWhiteSpace(documento) ? null : documento.Trim());
}
