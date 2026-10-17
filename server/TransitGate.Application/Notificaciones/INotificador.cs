using TransitGate.Application.Cuentas;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;
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
/// Eventos en tiempo real hacia la web (contrato C7; lo implementa Alex sobre SignalR). Cada método recibe lo
/// ya calculado (la vista o la entidad), no un id: se llaman con el cerrojo del torniquete tomado o justo
/// después, y con solo el id el notificador tendría que volver a buscarlo. Los demás eventos de §6 se agregan
/// a medida que existan.
/// </summary>
public interface INotificador
{
    Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado, CancellationToken ct = default);

    /// <summary>Cada intento, para el historial en vivo del panel (RF-32, HU-19 CA1).</summary>
    Task IntentoRegistradoAsync(IntentoAcceso intento, CancellationToken ct = default);

    /// <summary>Un colado nuevo (RF-33, HU-21).</summary>
    Task AlertaNuevaAsync(AlertaSeguridad alerta, CancellationToken ct = default);

    /// <summary>La alerta cambió de estado, por ejemplo al apagar la alarma (RF-34).</summary>
    Task AlertaActualizadaAsync(AlertaSeguridad alerta, CancellationToken ct = default);

    /// <summary>
    /// Resultado de una lectura en modo registro, para que la taquilla muestre "Tarjeta entregada" o
    /// "Tarjeta ya registrada" (RF-10 CA1 y CA2).
    /// </summary>
    Task RegistroTarjetaAsync(string torniqueteId, Guid solicitudId, ResultadoLigar resultado, CancellationToken ct = default);
}
