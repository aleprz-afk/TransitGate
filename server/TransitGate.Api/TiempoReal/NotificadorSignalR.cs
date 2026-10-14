using Microsoft.AspNetCore.SignalR;
using TransitGate.Api.Contratos;
using TransitGate.Application.Notificaciones;

namespace TransitGate.Api.TiempoReal;

public sealed class NotificadorSignalR(IHubContext<EventosHub> hub) : INotificador
{
    public const string EventoEstadoTorniquete = "EstadoTorniquete";

    public Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado, CancellationToken ct = default) =>
        hub.Clients.Group(EventosHub.GrupoOperacion)
            .SendAsync(EventoEstadoTorniquete, EstadoTorniqueteEvento.Desde(estado), ct);
}
