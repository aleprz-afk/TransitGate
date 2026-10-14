using System.Collections.Concurrent;
using TransitGate.Application.Validacion;

namespace TransitGate.Api.Esqueleto;

public sealed class LecturasProcesadasEnMemoria : ILecturasProcesadas
{
    private readonly ConcurrentDictionary<(string Torniquete, string LecturaId), ResultadoLectura> _lecturas = new();

    public Task<ResultadoLectura?> BuscarAsync(string torniqueteId, string lecturaId, CancellationToken ct = default) =>
        Task.FromResult(_lecturas.GetValueOrDefault((torniqueteId, lecturaId)));

    public Task GuardarAsync(string torniqueteId, string lecturaId, ResultadoLectura resultado, CancellationToken ct = default)
    {
        _lecturas[(torniqueteId, lecturaId)] = resultado;
        return Task.CompletedTask;
    }
}
