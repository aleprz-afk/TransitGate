using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Torniquetes;

public interface ICruceRepositorio
{
    Task<long> SiguienteIdAsync(CancellationToken ct = default);

    Task AgregarAsync(Cruce cruce, CancellationToken ct = default);

    /// <summary>Para la idempotencia por sesionVision + trackId (RF-26 CA3).</summary>
    Task<Cruce?> BuscarAsync(string torniqueteId, string sesionVision, long trackId, CancellationToken ct = default);
}
