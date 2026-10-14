using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;
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

    public Task<long> SiguientePasoIdAsync(CancellationToken ct = default) => Task.FromResult(++_ultimoPasoId);
}

internal sealed class LecturasFalsas : ILecturasProcesadas
{
    private readonly Dictionary<(string, string), ResultadoLectura> _lecturas = [];

    public Task<ResultadoLectura?> BuscarAsync(string torniqueteId, string lecturaId, CancellationToken ct = default) =>
        Task.FromResult(_lecturas.GetValueOrDefault((torniqueteId, lecturaId)));

    public Task GuardarAsync(string torniqueteId, string lecturaId, ResultadoLectura resultado, CancellationToken ct = default)
    {
        _lecturas[(torniqueteId, lecturaId)] = resultado;
        return Task.CompletedTask;
    }
}

internal sealed class ParametrosFijos : IParametrosOperacion
{
    public TimeSpan VentanaPaso { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan GraciaVision { get; set; } = TimeSpan.FromSeconds(1);
}

internal sealed class RelojFijo(DateTime ahoraUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(ahoraUtc, TimeSpan.Zero);
}
