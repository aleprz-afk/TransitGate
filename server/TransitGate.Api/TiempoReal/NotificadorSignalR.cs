using Microsoft.AspNetCore.SignalR;
using TransitGate.Api.Contratos;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;

namespace TransitGate.Api.TiempoReal;

public sealed class NotificadorSignalR(IHubContext<EventosHub> hub, TimeProvider reloj) : INotificador
{
    public const string EventoEstadoTorniquete = "EstadoTorniquete";
    public const string EventoIntentoRegistrado = "IntentoRegistrado";
    public const string EventoAlertaNueva = "AlertaNueva";
    public const string EventoAlertaActualizada = "AlertaActualizada";
    public const string EventoRegistroTarjeta = "RegistroTarjeta";
    public const string EventoSolicitudNueva = "SolicitudNueva";
    public const string EventoSaldoActualizado = "SaldoActualizado";
    public const string EventoViaje = "EventoViaje";

    public Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado, CancellationToken ct = default) =>
        AOperacion(EventoEstadoTorniquete, EstadoTorniqueteEvento.Desde(estado), ct);

    public Task IntentoRegistradoAsync(IntentoAcceso intento, CancellationToken ct = default) =>
        AOperacion(EventoIntentoRegistrado, IntentoRegistradoEvento.Desde(intento), ct);

    public Task AlertaNuevaAsync(AlertaSeguridad alerta, CancellationToken ct = default) =>
        AOperacion(EventoAlertaNueva, AlertaNuevaEvento.Desde(alerta), ct);

    public Task AlertaActualizadaAsync(AlertaSeguridad alerta, CancellationToken ct = default) =>
        AOperacion(EventoAlertaActualizada, AlertaActualizadaEvento.Desde(alerta), ct);

    public Task RegistroTarjetaAsync(string torniqueteId, Guid solicitudId, ResultadoLigar resultado, CancellationToken ct = default) =>
        AOperacion(
            EventoRegistroTarjeta,
            new RegistroTarjetaEvento(torniqueteId, IdsPublicos.Solicitud(solicitudId), resultado.ToString(), reloj.GetUtcNow().UtcDateTime),
            ct);

    public Task SolicitudNuevaAsync(SolicitudPendiente solicitud, CancellationToken ct = default) =>
        AOperacion(EventoSolicitudNueva, SolicitudNuevaEvento.Desde(solicitud), ct);

    public Task SaldoActualizadoAsync(Guid clienteId, decimal saldo, CausaSaldo causa, CancellationToken ct = default) =>
        ACliente(clienteId, EventoSaldoActualizado, new SaldoActualizadoEvento((int)saldo, causa.ToString(), reloj.GetUtcNow().UtcDateTime), ct);

    public Task EventoViajeAsync(Guid clienteId, IntentoAcceso intento, string torniqueteNombre, CancellationToken ct = default) =>
        ACliente(clienteId, EventoViaje, EventoViajeEvento.Desde(intento, torniqueteNombre), ct);

    // Mientras no haya sesión nadie entra a los grupos de cliente: el evento sale, pero no lo recibe nadie.
    private Task ACliente(Guid clienteId, string evento, object contenido, CancellationToken ct) =>
        hub.Clients.Group(EventosHub.GrupoCliente(clienteId)).SendAsync(evento, contenido, ct);

    private Task AOperacion(string evento, object contenido, CancellationToken ct) =>
        hub.Clients.Group(EventosHub.GrupoOperacion).SendAsync(evento, contenido, ct);
}
