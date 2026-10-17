using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TransitGate.Api.Contratos;
using TransitGate.Api.Seguridad;
using TransitGate.Application.Notificaciones;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Api.TiempoReal;

/// <summary>
/// Hub /hubs/eventos (contratos de integración §6), con la cookie de sesión. El personal entra al grupo
/// "operacion" y cada cliente al suyo, "cliente:{id}", donde solo le llegan los eventos de su tarjeta.
/// </summary>
[Authorize]
public sealed class EventosHub(MonitorTorniquetes monitor) : Hub
{
    public const string Ruta = "/hubs/eventos";
    public const string GrupoOperacion = "operacion";

    public static string GrupoCliente(Guid clienteId) => $"cliente:{clienteId:N}";

    public override async Task OnConnectedAsync()
    {
        var usuario = Context.User!;
        if (usuario.IsInRole(Roles.Cliente))
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoCliente(usuario.IdUsuario()));

        if (usuario.IsInRole(Roles.Operador) || usuario.IsInRole(Roles.Seguridad) || usuario.IsInRole(Roles.Administrador))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoOperacion);

            // Quien se conecta recibe enseguida el estado actual, sin esperar al próximo cambio.
            foreach (var estado in monitor.UltimosPublicados)
                await Clients.Caller.SendAsync(NotificadorSignalR.EventoEstadoTorniquete, EstadoTorniqueteEvento.Desde(estado));
        }

        await base.OnConnectedAsync();
    }
}
