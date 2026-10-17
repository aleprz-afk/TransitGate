using TransitGate.Application.Cuentas;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Api.Esqueleto;

// Repositorios del módulo de cuentas en memoria, hasta tener PostgreSQL. Cada uno guarda al instante;
// UnidadDeTrabajoEnMemoria no hace nada.

public sealed class ClienteRepositorioEnMemoria : IClienteRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<Cliente> _clientes = [];

    public Task AgregarAsync(Cliente cliente, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _clientes.Add(cliente);
        return Task.CompletedTask;
    }

    public Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_clientes.FirstOrDefault(c => c.Id == clienteId));
    }

    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_clientes.Any(c => c.Correo == correo));
    }

    public Task<bool> ExisteDocumentoAsync(string numeroDocumento, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_clientes.Any(c => c.NumeroDocumento == numeroDocumento));
    }
}

public sealed class TarjetaRepositorioEnMemoria : ITarjetaRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<Tarjeta> _tarjetas = [];

    public Task AgregarAsync(Tarjeta tarjeta, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _tarjetas.Add(tarjeta);
        return Task.CompletedTask;
    }

    public Task<Tarjeta?> ObtenerAsync(Guid tarjetaId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_tarjetas.FirstOrDefault(t => t.Id == tarjetaId));
    }

    public Task<Tarjeta?> DelClienteAsync(Guid clienteId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_tarjetas.FirstOrDefault(t => t.ClienteId == clienteId));
    }
}

public sealed class SoporteRepositorioEnMemoria : ISoporteRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<SoporteTarjeta> _soportes = [];

    public Task AgregarAsync(SoporteTarjeta soporte, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _soportes.Add(soporte);
        return Task.CompletedTask;
    }

    public Task<SoporteTarjeta?> ObtenerAsync(Guid soporteId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_soportes.FirstOrDefault(s => s.Id == soporteId));
    }

    public Task<SoporteTarjeta?> PorUidAsync(string uid, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_soportes.FirstOrDefault(s => s.Uid == uid));
    }

    public Task<IReadOnlyList<SoporteTarjeta>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult<IReadOnlyList<SoporteTarjeta>>([.. _soportes.Where(s => s.TarjetaId == tarjetaId)]);
    }

    public Task<IReadOnlyList<SoporteTarjeta>> SolicitadosAsync(CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult<IReadOnlyList<SoporteTarjeta>>(
                [.. _soportes.Where(s => s.Estado == EstadoSoporte.Solicitada).OrderBy(s => s.FechaSolicitud)]);
    }
}

public sealed class RecargaRepositorioEnMemoria : IRecargaRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly List<Recarga> _recargas = [];
    private long _ultimoId;

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _ultimoId));

    public Task AgregarAsync(Recarga recarga, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _recargas.Add(recarga);
        return Task.CompletedTask;
    }

    public Task<Recarga?> PorClaveAsync(Guid tarjetaId, string claveIdempotencia, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_recargas.FirstOrDefault(r => r.TarjetaId == tarjetaId && r.ClaveIdempotencia == claveIdempotencia));
    }

    public Task<IReadOnlyList<Recarga>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult<IReadOnlyList<Recarga>>(
                [.. _recargas.Where(r => r.TarjetaId == tarjetaId).OrderByDescending(r => r.Id)]);
    }
}

public sealed class DebitoRepositorioEnMemoria : IDebitoRepositorio
{
    private readonly Lock _cerrojo = new();
    private readonly Dictionary<long, DebitoPaso> _debitos = [];

    public Task AgregarAsync(DebitoPaso debito, CancellationToken ct = default)
    {
        lock (_cerrojo)
            _debitos[debito.PasoId] = debito;
        return Task.CompletedTask;
    }

    public Task<DebitoPaso?> PorPasoAsync(long pasoId, CancellationToken ct = default)
    {
        lock (_cerrojo)
            return Task.FromResult(_debitos.GetValueOrDefault(pasoId));
    }
}
