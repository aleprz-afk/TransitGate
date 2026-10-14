using Microsoft.AspNetCore.SignalR;
using TransitGate.Api.Contratos;
using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;

namespace TransitGate.Api.TiempoReal;

public sealed class NotificadorSignalR(IHubContext<EventosHub> hub) : INotificador
{
    public const string EventoEstadoTorniquete = "EstadoTorniquete";
    public const string EventoIntentoRegistrado = "IntentoRegistrado";
    public const string EventoAlertaNueva = "AlertaNueva";
    public const string EventoAlertaActualizada = "AlertaActualizada";

    public Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado, CancellationToken ct = default) =>
        AOperacion(EventoEstadoTorniquete, EstadoTorniqueteEvento.Desde(estado), ct);

    public Task IntentoRegistradoAsync(IntentoAcceso intento, CancellationToken ct = default) =>
        AOperacion(EventoIntentoRegistrado, IntentoRegistradoEvento.Desde(intento), ct);

    public Task AlertaNuevaAsync(AlertaSeguridad alerta, CancellationToken ct = default) =>
        AOperacion(EventoAlertaNueva, AlertaNuevaEvento.Desde(alerta), ct);

    public Task AlertaActualizadaAsync(AlertaSeguridad alerta, CancellationToken ct = default) =>
        AOperacion(EventoAlertaActualizada, AlertaActualizadaEvento.Desde(alerta), ct);

    private Task AOperacion(string evento, object contenido, CancellationToken ct) =>
        hub.Clients.Group(EventosHub.GrupoOperacion).SendAsync(evento, contenido, ct);
}
