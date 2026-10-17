using System.Collections.Concurrent;

namespace TransitGate.Application.Cuentas;

/// <summary>
/// Serializa los cambios de saldo de una misma tarjeta: un paso en el torniquete y una recarga desde el
/// portal pueden llegar a la vez (RNF-16), y dos clics seguidos en "Recargar" también (RF-15 CA5). Con
/// PostgreSQL se suma el control de concurrencia del saldo; este cerrojo basta para una sola laptop.
/// </summary>
public sealed class CerrojoTarjetas
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _semaforos = new();

    public async Task<IDisposable> TomarAsync(Guid tarjetaId, CancellationToken ct = default)
    {
        var semaforo = _semaforos.GetOrAdd(tarjetaId, _ => new SemaphoreSlim(1, 1));
        await semaforo.WaitAsync(ct);
        return new Liberador(semaforo);
    }

    private sealed class Liberador(SemaphoreSlim semaforo) : IDisposable
    {
        public void Dispose() => semaforo.Release();
    }
}
