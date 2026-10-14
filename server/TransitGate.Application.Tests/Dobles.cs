using Microsoft.Extensions.Logging.Abstractions;
using TransitGate.Application.Accesos;
using TransitGate.Application.Alertas;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Notificaciones;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests;

// Dobles de prueba de los puertos de Application.

internal sealed class CuentasFalsas : ISoporteResolver, ISaldoService
{
    private readonly Dictionary<string, SoporteResuelto> _soportes = [];
    private readonly Dictionary<Guid, decimal> _saldos = [];

    public decimal Tarifa { get; set; } = 3200;

    public List<(Guid TarjetaId, decimal Monto, long PasoId)> Debitos { get; } = [];

    public SoporteResuelto Agregar(string uid, decimal saldo, EstadoSoporte estado = EstadoSoporte.Activa)
    {
        var soporte = new SoporteResuelto(Guid.NewGuid(), Guid.NewGuid(), estado);
        _soportes[uid] = soporte;
        _saldos[soporte.TarjetaId] = saldo;
        return soporte;
    }

    public decimal SaldoDe(SoporteResuelto soporte) => _saldos[soporte.TarjetaId];

    public Task<SoporteResuelto?> PorUidAsync(string uidHex) => Task.FromResult(_soportes.GetValueOrDefault(uidHex));

    public Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId) =>
        Task.FromResult(_soportes.Values.FirstOrDefault(s => s.SoporteId == soporteId));

    public Task<bool> VerificarFirmaAsync(Guid soporteId, string torniqueteId, byte[] desafio, byte[] firma) =>
        Task.FromResult(false);

    public Task<decimal> TarifaVigenteAsync() => Task.FromResult(Tarifa);

    public Task<ResultadoDebito> DebitarAsync(Guid tarjetaId, decimal monto, long pasoId)
    {
        var saldo = _saldos[tarjetaId];
        if (saldo < monto)
            return Task.FromResult(new ResultadoDebito(false, saldo));

        _saldos[tarjetaId] = saldo - monto;
        Debitos.Add((tarjetaId, monto, pasoId));
        return Task.FromResult(new ResultadoDebito(true, saldo - monto));
    }

    public Task RevertirAsync(long pasoId) => throw new NotImplementedException();
}

internal sealed class TorniquetesFalsos(Torniquete torniquete) : ITorniqueteRepositorio
{
    private long _ultimoPasoId;

    public Task<Torniquete?> ObtenerAsync(string torniqueteId, CancellationToken ct = default) =>
        Task.FromResult(torniqueteId == torniquete.Id ? torniquete : null);

    public Task<IReadOnlyList<Torniquete>> ListarAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Torniquete>>([torniquete]);

    public Task<long> SiguientePasoIdAsync(CancellationToken ct = default) => Task.FromResult(++_ultimoPasoId);
}

internal sealed class IntentosFalsos : IIntentoRepositorio
{
    private long _ultimoId;

    public List<IntentoAcceso> Guardados { get; } = [];

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => Task.FromResult(++_ultimoId);

    public Task AgregarAsync(IntentoAcceso intento, CancellationToken ct = default)
    {
        Guardados.Add(intento);
        return Task.CompletedTask;
    }

    public Task<IntentoAcceso?> BuscarPorLecturaAsync(string torniqueteId, string lecturaId, CancellationToken ct = default) =>
        Task.FromResult(Guardados.FirstOrDefault(i => i.TorniqueteId == torniqueteId && i.LecturaId == lecturaId));

    public Task<IReadOnlyList<IntentoAcceso>> ListarPorTorniqueteAsync(string torniqueteId, int limite, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<IntentoAcceso>>(
            [.. Guardados.Where(i => i.TorniqueteId == torniqueteId).OrderByDescending(i => i.Id).Take(limite)]);
}

internal sealed class UnidadDeTrabajoFalsa : IUnidadDeTrabajo
{
    public int VecesGuardada { get; private set; }

    public Task GuardarCambiosAsync(CancellationToken ct = default)
    {
        VecesGuardada++;
        return Task.CompletedTask;
    }
}

internal sealed class ParametrosFijos : IParametrosOperacion
{
    public TimeSpan VentanaPaso { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan GraciaVision { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan PlazoSenalEsp32 { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan PlazoSenalVision { get; set; } = TimeSpan.FromSeconds(15);
}

internal sealed class NotificadorFalso : INotificador
{
    public List<EstadoTorniqueteVista> Enviados { get; } = [];

    public List<IntentoAcceso> Intentos { get; } = [];

    public List<AlertaSeguridad> AlertasNuevas { get; } = [];

    public List<AlertaSeguridad> AlertasActualizadas { get; } = [];

    public Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado, CancellationToken ct = default)
    {
        Enviados.Add(estado);
        return Task.CompletedTask;
    }

    public Task IntentoRegistradoAsync(IntentoAcceso intento, CancellationToken ct = default)
    {
        Intentos.Add(intento);
        return Task.CompletedTask;
    }

    public Task AlertaNuevaAsync(AlertaSeguridad alerta, CancellationToken ct = default)
    {
        AlertasNuevas.Add(alerta);
        return Task.CompletedTask;
    }

    public Task AlertaActualizadaAsync(AlertaSeguridad alerta, CancellationToken ct = default)
    {
        AlertasActualizadas.Add(alerta);
        return Task.CompletedTask;
    }
}

internal sealed class CrucesFalsos : ICruceRepositorio
{
    private long _ultimoId;

    public List<Cruce> Guardados { get; } = [];

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => Task.FromResult(++_ultimoId);

    public Task AgregarAsync(Cruce cruce, CancellationToken ct = default)
    {
        Guardados.Add(cruce);
        return Task.CompletedTask;
    }

    public Task<Cruce?> BuscarAsync(string torniqueteId, string sesionVision, long trackId, CancellationToken ct = default) =>
        Task.FromResult(Guardados.FirstOrDefault(c =>
            c.TorniqueteId == torniqueteId && c.SesionVision == sesionVision && c.TrackId == trackId));
}

internal sealed class AlertasFalsas : IAlertaRepositorio
{
    private long _ultimoId;

    public List<AlertaSeguridad> Guardadas { get; } = [];

    public Task<long> SiguienteIdAsync(CancellationToken ct = default) => Task.FromResult(++_ultimoId);

    public Task AgregarAsync(AlertaSeguridad alerta, CancellationToken ct = default)
    {
        Guardadas.Add(alerta);
        return Task.CompletedTask;
    }

    public Task<AlertaSeguridad?> ObtenerAsync(long alertaId, CancellationToken ct = default) =>
        Task.FromResult(Guardadas.FirstOrDefault(a => a.Id == alertaId));

    public Task<AlertaSeguridad?> AbiertaDeAsync(string torniqueteId, CancellationToken ct = default) =>
        Task.FromResult(Guardadas.FirstOrDefault(a => a.TorniqueteId == torniqueteId && a.Estado == EstadoAlerta.Abierta));

    public Task<IReadOnlyList<AlertaSeguridad>> ListarAsync(EstadoAlerta? estado, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AlertaSeguridad>>(
            [.. Guardadas.Where(a => estado is null || a.Estado == estado).OrderByDescending(a => a.Id)]);
}

internal sealed class RelojFijo(DateTime ahoraUtc) : TimeProvider
{
    public DateTime Ahora { get; set; } = ahoraUtc;

    public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
}

internal static class Bitacora
{
    public static BitacoraTorniquete Nula() => new(NullLogger<BitacoraTorniquete>.Instance);
}
