using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Torniquetes;

public interface ITorniqueteRepositorio
{
    Task<Torniquete?> ObtenerAsync(string torniqueteId, CancellationToken ct = default);

    /// <summary>El id del paso se reserva antes de debitar, porque DebitarAsync lo necesita (C7).</summary>
    Task<long> SiguientePasoIdAsync(CancellationToken ct = default);
}
