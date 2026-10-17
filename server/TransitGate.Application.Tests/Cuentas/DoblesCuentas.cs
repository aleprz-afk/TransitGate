using TransitGate.Application.Cuentas;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Tests.Cuentas;

// Repositorios del módulo de cuentas en listas, para probar los casos de uso sin base de datos.

internal sealed class ClientesFalsos : IClienteRepositorio
{
    public List<Cliente> Guardados { get; } = [];

    public Task AgregarAsync(Cliente cliente, CancellationToken ct = default)
    {
        Guardados.Add(cliente);
        return Task.CompletedTask;
    }

    public Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken ct = default) =>
        Task.FromResult(Guardados.FirstOrDefault(c => c.Id == clienteId));

    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken ct = default) =>
        Task.FromResult(Guardados.Any(c => c.Correo == correo));

    public Task<bool> ExisteDocumentoAsync(string numeroDocumento, CancellationToken ct = default) =>
        Task.FromResult(Guardados.Any(c => c.NumeroDocumento == numeroDocumento));
}

internal sealed class TarjetasFalsas : ITarjetaRepositorio
{
    public List<Tarjeta> Guardadas { get; } = [];

    public Task AgregarAsync(Tarjeta tarjeta, CancellationToken ct = default)
    {
        Guardadas.Add(tarjeta);
        return Task.CompletedTask;
    }

    public Task<Tarjeta?> ObtenerAsync(Guid tarjetaId, CancellationToken ct = default) =>
        Task.FromResult(Guardadas.FirstOrDefault(t => t.Id == tarjetaId));

    public Task<Tarjeta?> DelClienteAsync(Guid clienteId, CancellationToken ct = default) =>
        Task.FromResult(Guardadas.FirstOrDefault(t => t.ClienteId == clienteId));

    public Task<Guid?> ClienteDeAsync(Guid tarjetaId, CancellationToken ct = default) =>
        Task.FromResult(Guardadas.FirstOrDefault(t => t.Id == tarjetaId)?.ClienteId);
}

internal sealed class SoportesFalsos : ISoporteRepositorio
{
    public List<SoporteTarjeta> Guardados { get; } = [];

    public Task AgregarAsync(SoporteTarjeta soporte, CancellationToken ct = default)
    {
        Guardados.Add(soporte);
        return Task.CompletedTask;
    }

    public Task<SoporteTarjeta?> ObtenerAsync(Guid soporteId, CancellationToken ct = default) =>
        Task.FromResult(Guardados.FirstOrDefault(s => s.Id == soporteId));

    public Task<SoporteTarjeta?> PorUidAsync(string uid, CancellationToken ct = default) =>
        Task.FromResult(Guardados.FirstOrDefault(s => s.Uid == uid));

    public Task<IReadOnlyList<SoporteTarjeta>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SoporteTarjeta>>([.. Guardados.Where(s => s.TarjetaId == tarjetaId)]);

    public Task<IReadOnlyList<SoporteTarjeta>> SolicitadosAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SoporteTarjeta>>(
            [.. Guardados.Where(s => s.Estado == EstadoSoporte.Solicitada).OrderBy(s => s.FechaSolicitud)]);
}

internal sealed class RecargasFalsas : IRecargaRepositorio
{
    private long _ultimoId;

    public List<Recarga> Guardadas { get; } = [];

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => Task.FromResult(Interlocked.Increment(ref _ultimoId));

    public Task AgregarAsync(Recarga recarga, CancellationToken ct = default)
    {
        lock (Guardadas)
            Guardadas.Add(recarga);
        return Task.CompletedTask;
    }

    public Task<Recarga?> PorClaveAsync(Guid tarjetaId, string claveIdempotencia, CancellationToken ct = default)
    {
        lock (Guardadas)
            return Task.FromResult(Guardadas.FirstOrDefault(r => r.TarjetaId == tarjetaId && r.ClaveIdempotencia == claveIdempotencia));
    }

    public Task<IReadOnlyList<Recarga>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Recarga>>([.. Guardadas.Where(r => r.TarjetaId == tarjetaId).OrderByDescending(r => r.Id)]);
}

internal sealed class DebitosFalsos : IDebitoRepositorio
{
    public Dictionary<long, DebitoPaso> Guardados { get; } = [];

    public Task AgregarAsync(DebitoPaso debito, CancellationToken ct = default)
    {
        Guardados[debito.PasoId] = debito;
        return Task.CompletedTask;
    }

    public Task<DebitoPaso?> PorPasoAsync(long pasoId, CancellationToken ct = default) =>
        Task.FromResult(Guardados.GetValueOrDefault(pasoId));
}

internal sealed class ParametrosCuentasFijos : IParametrosCuentas
{
    public decimal Tarifa { get; set; } = 3200;

    public decimal MontoMinRecarga { get; set; } = 1000;

    public decimal MontoMaxRecarga { get; set; } = 200000;
}

internal sealed class CuentasAccesoFalsas : ICuentasAcceso
{
    public List<(Guid ClienteId, string Correo)> Creadas { get; } = [];

    public ErrorCuenta? Error { get; set; }

    public Task<ErrorCuenta?> CrearClienteAsync(Guid clienteId, string correo, string password, CancellationToken ct = default)
    {
        if (Error is null)
            Creadas.Add((clienteId, correo));
        return Task.FromResult(Error);
    }
}

/// <summary>El módulo de cuentas completo sobre listas, con un reloj que se puede mover.</summary>
internal sealed class ModuloCuentasFalso
{
    public static readonly DateTime Ahora = new(2026, 10, 16, 13, 0, 0, DateTimeKind.Utc);

    public ClientesFalsos Clientes { get; } = new();
    public TarjetasFalsas Tarjetas { get; } = new();
    public SoportesFalsos Soportes { get; } = new();
    public RecargasFalsas Recargas { get; } = new();
    public DebitosFalsos Debitos { get; } = new();
    public ParametrosCuentasFijos Parametros { get; } = new();
    public CuentasAccesoFalsas CuentasAcceso { get; } = new();
    public UnidadDeTrabajoFalsa UnidadDeTrabajo { get; } = new();
    public NotificadorFalso Notificador { get; } = new();
    public CerrojoTarjetas Cerrojo { get; } = new();
    public RelojFijo Reloj { get; } = new(Ahora);

    public ServicioCuentas Servicio() => new(Soportes, Tarjetas, Clientes, Debitos, Parametros, Cerrojo, Reloj);

    /// <summary>Un cliente con su tarjeta y, si se indica, una tarjeta física en ese estado.</summary>
    public (Cliente Cliente, Tarjeta Tarjeta, SoporteTarjeta? Soporte) Cliente(
        decimal saldo = 0, EstadoSoporte? estadoFisico = null, string uid = "04A1B2C3", string documento = "1032456789")
    {
        var cliente = new Cliente(Guid.NewGuid(), "Luisa Rojas", "CC", documento, $"{documento}@example.com", "3001234567", Ahora);
        var tarjeta = new Tarjeta(Guid.NewGuid(), cliente.Id);
        if (saldo > 0)
            tarjeta.Acreditar(saldo);
        Clientes.Guardados.Add(cliente);
        Tarjetas.Guardadas.Add(tarjeta);

        if (estadoFisico is not { } estado)
            return (cliente, tarjeta, null);

        var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), tarjeta.Id, Ahora);
        if (estado != EstadoSoporte.Solicitada)
            soporte.Entregar(uid, Ahora);
        if (estado is EstadoSoporte.Activa or EstadoSoporte.Bloqueada)
            soporte.Activar(Ahora);
        if (estado == EstadoSoporte.Bloqueada)
            soporte.Bloquear(MotivoBloqueo.Perdida, Ahora);
        Soportes.Guardados.Add(soporte);
        return (cliente, tarjeta, soporte);
    }
}
