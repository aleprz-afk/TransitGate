using System.Collections.Concurrent;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Notificaciones;

/// <summary>
/// Revisa los torniquetes cada medio segundo: aplica los vencimientos aunque no llegue ningún latido
/// (RF-29) y publica EstadoTorniquete cuando cambia el estado, la conexión (RF-37) o la sincronización
/// (RF-45 CA4). Revisar cada 500 ms deja margen para los 2 s de RNF-05.
/// </summary>
public sealed class MonitorTorniquetes(
    ITorniqueteRepositorio torniquetes,
    CerrojoTorniquetes cerrojo,
    IParametrosOperacion parametros,
    INotificador notificador,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(500);

    // RF-45 CA4: más de 2 s con el estado reportado distinto del deseado.
    private static readonly TimeSpan ToleranciaDesincronizacion = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, EstadoTorniqueteVista> _publicados = new();

    /// <summary>El último estado publicado de cada torniquete, para quien se conecta al hub.</summary>
    public IReadOnlyCollection<EstadoTorniqueteVista> UltimosPublicados => [.. _publicados.Values];

    public async Task RevisarAsync(CancellationToken ct = default)
    {
        foreach (var torniquete in await torniquetes.ListarAsync(ct))
        {
            EstadoTorniqueteVista vista;
            using (await cerrojo.TomarAsync(torniquete.Id, ct))
            {
                var ahora = reloj.GetUtcNow().UtcDateTime;
                var antes = torniquete.Deseado;
                torniquete.ActualizarPorTiempo(ahora);
                bitacora.SiCambioElDeseado(torniquete, antes, "vencimiento");
                vista = Vista(torniquete, ahora);
            }

            if (_publicados.TryGetValue(torniquete.Id, out var anterior) && anterior == vista)
                continue;

            _publicados[torniquete.Id] = vista;
            await notificador.EstadoTorniqueteAsync(vista, ct);
        }
    }

    private EstadoTorniqueteVista Vista(Torniquete torniquete, DateTime ahora)
    {
        var esp32Conectado = torniquete.Esp32Conectado(ahora, parametros.PlazoSenalEsp32);

        return new EstadoTorniqueteVista(
            torniquete.Id,
            torniquete.Estado,
            esp32Conectado,
            torniquete.VisionConectada(ahora, parametros.PlazoSenalVision),
            // Sin conexión no tiene sentido hablar de desincronización: eso ya lo dice esp32Conectado.
            esp32Conectado && torniquete.Desincronizado(ahora, ToleranciaDesincronizacion));
    }
}
