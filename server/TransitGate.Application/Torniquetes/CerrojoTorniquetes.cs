using System.Collections.Concurrent;

namespace TransitGate.Application.Torniquetes;

/// <summary>
/// Serializa las operaciones sobre un mismo torniquete (lectura, latido, cruce), que llegan en
/// paralelo desde el ESP32 y la visión. El servidor es una sola laptop, así que basta un cerrojo en memoria.
/// </summary>
public sealed class CerrojoTorniquetes
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaforos = new();

    public async Task<IDisposable> TomarAsync(string torniqueteId, CancellationToken ct = default)
    {
        var semaforo = _semaforos.GetOrAdd(torniqueteId, _ => new SemaphoreSlim(1, 1));
        await semaforo.WaitAsync(ct);
        return new Liberador(semaforo);
    }

    private sealed class Liberador(SemaphoreSlim semaforo) : IDisposable
    {
        public void Dispose() => semaforo.Release();
    }
}
