using TransitGate.Application;
using TransitGate.Application.Accesos;
using TransitGate.Domain.Accesos;

namespace TransitGate.Api.Esqueleto;

public sealed class IntentoRepositorioEnMemoria : IIntentoRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<IntentoAcceso> _intentos = [];
    private long _ultimoId;

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _ultimoId));

    public Task AgregarAsync(IntentoAcceso intento, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _intentos.Add(intento);

        return Task.CompletedTask;
    }

    public Task<IntentoAcceso?> BuscarPorLecturaAsync(string torniqueteId, string lecturaId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_intentos.FirstOrDefault(i => i.TorniqueteId == torniqueteId && i.LecturaId == lecturaId));
    }

    public Task<IReadOnlyList<IntentoAcceso>> ListarPorTorniqueteAsync(string torniqueteId, int limite, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            IReadOnlyList<IntentoAcceso> recientes =
                [.. _intentos.Where(i => i.TorniqueteId == torniqueteId).OrderByDescending(i => i.Id).Take(limite)];
            return Task.FromResult(recientes);
        }
    }
}

/// <summary>En memoria cada repositorio guarda al instante; con EF Core aquí va el único SaveChanges (RNF-15).</summary>
public sealed class UnidadDeTrabajoEnMemoria : IUnidadDeTrabajo
{
    public Task GuardarCambiosAsync(CancellationToken ct = default) => Task.CompletedTask;
}
