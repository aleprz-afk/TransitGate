using TransitGate.Application.Notificaciones;

namespace TransitGate.Api.TiempoReal;

/// <summary>Corre MonitorTorniquetes cada medio segundo mientras la API está en marcha.</summary>
public sealed class ServicioMonitorTorniquetes(
    MonitorTorniquetes monitor,
    ILogger<ServicioMonitorTorniquetes> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(MonitorTorniquetes.Intervalo);

        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await monitor.RevisarAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un fallo en una revisión no debe detener el monitor (RNF-14: 0 reinicios manuales).
                logger.LogError(ex, "Falló la revisión de los torniquetes");
            }
        }
    }
}
