using TransitGate.Application.Alertas;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Esqueleto;

public sealed class CruceRepositorioEnMemoria : ICruceRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<Cruce> _cruces = [];
    private long _ultimoId;

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _ultimoId));

    public Task AgregarAsync(Cruce cruce, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _cruces.Add(cruce);

        return Task.CompletedTask;
    }

    public Task<Cruce?> BuscarAsync(string torniqueteId, string sesionVision, long trackId, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            return Task.FromResult(_cruces.FirstOrDefault(c =>
                c.TorniqueteId == torniqueteId && c.SesionVision == sesionVision && c.TrackId == trackId));
        }
    }
}

public sealed class AlertaRepositorioEnMemoria : IAlertaRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<AlertaSeguridad> _alertas = [];
    private long _ultimoId;

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _ultimoId));

    public Task AgregarAsync(AlertaSeguridad alerta, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _alertas.Add(alerta);

        return Task.CompletedTask;
    }

    public Task<AlertaSeguridad?> ObtenerAsync(long alertaId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_alertas.FirstOrDefault(a => a.Id == alertaId));
    }

    public Task<AlertaSeguridad?> AbiertaDeAsync(string torniqueteId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_alertas.FirstOrDefault(a => a.TorniqueteId == torniqueteId && a.Estado == EstadoAlerta.Abierta));
    }

    public Task<IReadOnlyList<AlertaSeguridad>> ListarAsync(EstadoAlerta? estado, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            IReadOnlyList<AlertaSeguridad> lista =
                [.. _alertas.Where(a => estado is null || a.Estado == estado).OrderByDescending(a => a.Id)];
            return Task.FromResult(lista);
        }
    }
}
