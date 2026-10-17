using Microsoft.AspNetCore.SignalR;
using TransitGate.Api.Contratos;
using TransitGate.Application.Notificaciones;

namespace TransitGate.Api.TiempoReal;

/// <summary>
/// Hub /hubs/eventos (contratos de integración §6). Por ahora no exige sesión y toda conexión entra al
/// grupo "operacion"; con Identity, el grupo dependerá del rol y los clientes irán a "cliente:{id}".
/// </summary>
public sealed class EventosHub(MonitorTorniquetes monitor) : Hub
{
    public const string Ruta = "/hubs/eventos";
    public const string GrupoOperacion = "operacion";

    public static string GrupoCliente(Guid clienteId) => $"cliente:{clienteId:N}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoOperacion);

        // Quien se conecta recibe enseguida el estado actual, sin esperar al próximo cambio.
        foreach (var estado in monitor.UltimosPublicados)
            await Clients.Caller.SendAsync(NotificadorSignalR.EventoEstadoTorniquete, EstadoTorniqueteEvento.Desde(estado));

        await base.OnConnectedAsync();
    }
}
