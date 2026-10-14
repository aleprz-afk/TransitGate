using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Esqueleto;

/// <summary>Torniquete semilla tq_1 (MVP §3.3) en memoria, hasta tener la base de datos.</summary>
public sealed class TorniqueteRepositorioEnMemoria : ITorniqueteRepositorio
{
    public const string TorniqueteSemilla = "tq_1";

    private readonly Dictionary<string, Torniquete> _torniquetes = new()
    {
        [TorniqueteSemilla] = new Torniquete(TorniqueteSemilla, "Torniquete 1"),
    };
    private long _ultimoPasoId;

    public Task<Torniquete?> ObtenerAsync(string torniqueteId, CancellationToken ct = default) =>
        Task.FromResult(_torniquetes.GetValueOrDefault(torniqueteId));

    public Task<long> SiguientePasoIdAsync(CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _ultimoPasoId));
}
