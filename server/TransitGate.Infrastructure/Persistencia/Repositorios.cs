using Microsoft.EntityFrameworkCore;
using TransitGate.Application.Accesos;
using TransitGate.Application.Alertas;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Infrastructure.Persistencia;

// Repositorios sobre el mismo DbContext de la petición: agregan y buscan, y el caso de uso guarda todo junto
// con IUnidadDeTrabajo (RNF-15). Lo que solo se lee va sin seguimiento de cambios.

public sealed class IntentoRepositorio(TransitGateDbContext db, Secuencias secuencias) : IIntentoRepositorio
{
    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => secuencias.SiguienteAsync(Secuencias.Intentos, ct);

    public async Task AgregarAsync(IntentoAcceso intento, CancellationToken ct = default) =>
        await db.Intentos.AddAsync(intento, ct);

    public Task<IntentoAcceso?> BuscarPorLecturaAsync(string torniqueteId, string lecturaId, CancellationToken ct = default) =>
        db.Intentos.AsNoTracking().FirstOrDefaultAsync(i => i.TorniqueteId == torniqueteId && i.LecturaId == lecturaId, ct);

    public async Task<IReadOnlyList<IntentoAcceso>> ListarPorTorniqueteAsync(string torniqueteId, int limite, CancellationToken ct = default) =>
        await db.Intentos.AsNoTracking().Where(i => i.TorniqueteId == torniqueteId)
            .OrderByDescending(i => i.Id).Take(limite).ToListAsync(ct);

    public async Task<IReadOnlyList<IntentoAcceso>> ListarPorTarjetaAsync(Guid tarjetaId, int limite, CancellationToken ct = default) =>
        await db.Intentos.AsNoTracking().Where(i => i.TarjetaId == tarjetaId)
            .OrderByDescending(i => i.Id).Take(limite).ToListAsync(ct);
}

public sealed class CruceRepositorio(TransitGateDbContext db, Secuencias secuencias) : ICruceRepositorio
{
    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => secuencias.SiguienteAsync(Secuencias.Cruces, ct);

    public async Task AgregarAsync(Cruce cruce, CancellationToken ct = default) => await db.Cruces.AddAsync(cruce, ct);

    public Task<Cruce?> BuscarAsync(string torniqueteId, string sesionVision, long trackId, CancellationToken ct = default) =>
        db.Cruces.AsNoTracking().FirstOrDefaultAsync(
            c => c.TorniqueteId == torniqueteId && c.SesionVision == sesionVision && c.TrackId == trackId, ct);
}

public sealed class AlertaRepositorio(TransitGateDbContext db, Secuencias secuencias) : IAlertaRepositorio
{
    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => secuencias.SiguienteAsync(Secuencias.Alertas, ct);

    public async Task AgregarAsync(AlertaSeguridad alerta, CancellationToken ct = default) => await db.Alertas.AddAsync(alerta, ct);

    public async Task<AlertaSeguridad?> ObtenerAsync(long alertaId, CancellationToken ct = default) =>
        await db.Alertas.FindAsync([alertaId], ct);

    public Task<AlertaSeguridad?> AbiertaDeAsync(string torniqueteId, CancellationToken ct = default) =>
        db.Alertas.FirstOrDefaultAsync(a => a.TorniqueteId == torniqueteId && a.Estado == EstadoAlerta.Abierta, ct);

    public async Task<IReadOnlyList<AlertaSeguridad>> ListarAsync(EstadoAlerta? estado, CancellationToken ct = default) =>
        await db.Alertas.AsNoTracking().Where(a => estado == null || a.Estado == estado)
            .OrderByDescending(a => a.Id).ToListAsync(ct);
}

public sealed class ClienteRepositorio(TransitGateDbContext db) : IClienteRepositorio
{
    public async Task AgregarAsync(Cliente cliente, CancellationToken ct = default) => await db.Clientes.AddAsync(cliente, ct);

    public async Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken ct = default) =>
        await db.Clientes.FindAsync([clienteId], ct);

    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken ct = default) =>
        db.Clientes.AnyAsync(c => c.Correo == correo, ct);

    public Task<bool> ExisteDocumentoAsync(string numeroDocumento, CancellationToken ct = default) =>
        db.Clientes.AnyAsync(c => c.NumeroDocumento == numeroDocumento, ct);
}

public sealed class TarjetaRepositorio(TransitGateDbContext db) : ITarjetaRepositorio
{
    public async Task AgregarAsync(Tarjeta tarjeta, CancellationToken ct = default) => await db.Tarjetas.AddAsync(tarjeta, ct);

    // Con seguimiento: quien la pide va a cambiar el saldo. Si ya estaba cargada en esta petición, se relee,
    // porque el CerrojoTarjetas garantiza que nadie más la está cambiando en este momento (RNF-16).
    public async Task<Tarjeta?> ObtenerAsync(Guid tarjetaId, CancellationToken ct = default)
    {
        if (db.Tarjetas.Local.FirstOrDefault(t => t.Id == tarjetaId) is { } cargada)
        {
            if (db.Entry(cargada).State == EntityState.Unchanged)
                await db.Entry(cargada).ReloadAsync(ct);
            return cargada;
        }

        return await db.Tarjetas.FirstOrDefaultAsync(t => t.Id == tarjetaId, ct);
    }

    public Task<Tarjeta?> DelClienteAsync(Guid clienteId, CancellationToken ct = default) =>
        db.Tarjetas.AsNoTracking().FirstOrDefaultAsync(t => t.ClienteId == clienteId, ct);

    public Task<Guid?> ClienteDeAsync(Guid tarjetaId, CancellationToken ct = default) =>
        db.Tarjetas.Where(t => t.Id == tarjetaId).Select(t => (Guid?)t.ClienteId).FirstOrDefaultAsync(ct);
}

public sealed class SoporteRepositorio(TransitGateDbContext db) : ISoporteRepositorio
{
    public async Task AgregarAsync(SoporteTarjeta soporte, CancellationToken ct = default) => await db.Soportes.AddAsync(soporte, ct);

    public async Task<SoporteTarjeta?> ObtenerAsync(Guid soporteId, CancellationToken ct = default) =>
        await db.Soportes.FindAsync([soporteId], ct);

    public Task<SoporteTarjeta?> PorUidAsync(string uid, CancellationToken ct = default) =>
        db.Soportes.FirstOrDefaultAsync(s => s.Uid == uid, ct);

    public async Task<IReadOnlyList<SoporteTarjeta>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default) =>
        await db.Soportes.Where(s => s.TarjetaId == tarjetaId).ToListAsync(ct);

    public async Task<IReadOnlyList<SoporteTarjeta>> SolicitadosAsync(CancellationToken ct = default) =>
        await db.Soportes.AsNoTracking().Where(s => s.Estado == EstadoSoporte.Solicitada)
            .OrderBy(s => s.FechaSolicitud).ToListAsync(ct);
}

public sealed class RecargaRepositorio(TransitGateDbContext db, Secuencias secuencias) : IRecargaRepositorio
{
    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => secuencias.SiguienteAsync(Secuencias.Recargas, ct);

    public async Task AgregarAsync(Recarga recarga, CancellationToken ct = default) => await db.Recargas.AddAsync(recarga, ct);

    public Task<Recarga?> PorClaveAsync(Guid tarjetaId, string claveIdempotencia, CancellationToken ct = default) =>
        db.Recargas.AsNoTracking().FirstOrDefaultAsync(r => r.TarjetaId == tarjetaId && r.ClaveIdempotencia == claveIdempotencia, ct);

    public async Task<IReadOnlyList<Recarga>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default) =>
        await db.Recargas.AsNoTracking().Where(r => r.TarjetaId == tarjetaId).OrderByDescending(r => r.Id).ToListAsync(ct);
}

public sealed class DebitoRepositorio(TransitGateDbContext db) : IDebitoRepositorio
{
    public async Task AgregarAsync(DebitoPaso debito, CancellationToken ct = default) => await db.Debitos.AddAsync(debito, ct);

    public async Task<DebitoPaso?> PorPasoAsync(long pasoId, CancellationToken ct = default) =>
        await db.Debitos.FindAsync([pasoId], ct);
}

/// <summary>
/// Los torniquetes viven en memoria: son la máquina de estados que el ESP32 consulta cada 300 ms, y el
/// estado se reconstruye solo al reiniciar (arranca en Cerrado, RF-45 CA3). Solo el id del paso sale de la base.
/// </summary>
public sealed class TorniquetesEnMemoria(Secuencias secuencias) : ITorniqueteRepositorio
{
    public const string TorniqueteSemilla = "tq_1";

    private readonly Dictionary<string, Torniquete> _torniquetes = new()
    {
        [TorniqueteSemilla] = new Torniquete(TorniqueteSemilla, "Torniquete 1"),
    };

    public Task<Torniquete?> ObtenerAsync(string torniqueteId, CancellationToken ct = default) =>
        Task.FromResult(_torniquetes.GetValueOrDefault(torniqueteId));

    public Task<IReadOnlyList<Torniquete>> ListarAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Torniquete>>([.. _torniquetes.Values]);

    public Task<long> SiguientePasoIdAsync(CancellationToken ct = default) => secuencias.SiguienteAsync(Secuencias.Pasos, ct);
}
