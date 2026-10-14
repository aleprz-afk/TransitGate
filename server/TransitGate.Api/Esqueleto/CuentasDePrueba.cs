using Microsoft.Extensions.Options;
using TransitGate.Application.Cuentas;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Api.Esqueleto;

/// <summary>Tarjetas y tarifa de prueba (sección DatosDePrueba de appsettings.json).</summary>
public sealed class DatosDePrueba
{
    public const string Seccion = "DatosDePrueba";

    public decimal Tarifa { get; set; } = 3200;

    public List<TarjetaDePrueba> Tarjetas { get; set; } = [];
}

public sealed class TarjetaDePrueba
{
    public string Uid { get; set; } = "";

    public EstadoSoporte Estado { get; set; } = EstadoSoporte.Activa;

    public decimal Saldo { get; set; }
}

/// <summary>
/// Implementación falsa del contrato C7 mientras Jeisy conecta la base de datos (contratos de integración §7).
/// El saldo baja de verdad con cada paso, pero se pierde al reiniciar la API.
/// </summary>
public sealed class CuentasDePrueba : ISoporteResolver, ISaldoService
{
    private readonly Lock _cerrojo = new();
    private readonly decimal _tarifa;
    private readonly Dictionary<string, Cuenta> _porUid;
    private readonly Dictionary<long, (Cuenta Cuenta, decimal Monto)> _debitos = [];

    public CuentasDePrueba(IOptions<DatosDePrueba> opciones)
    {
        _tarifa = opciones.Value.Tarifa;
        _porUid = opciones.Value.Tarjetas.ToDictionary(
            t => t.Uid.ToUpperInvariant(),
            t => new Cuenta(new SoporteResuelto(Guid.NewGuid(), Guid.NewGuid(), t.Estado), t.Saldo));
    }

    public Task<SoporteResuelto?> PorUidAsync(string uidHex) =>
        Task.FromResult(_porUid.GetValueOrDefault(uidHex)?.Soporte);

    public Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId) =>
        Task.FromResult(_porUid.Values.FirstOrDefault(c => c.Soporte.SoporteId == soporteId)?.Soporte);

    public Task<bool> VerificarFirmaAsync(Guid soporteId, string torniqueteId, byte[] desafio, byte[] firma) =>
        Task.FromResult(false);

    public Task<decimal> TarifaVigenteAsync() => Task.FromResult(_tarifa);

    public Task<ResultadoDebito> DebitarAsync(Guid tarjetaId, decimal monto, long pasoId)
    {
        lock (_cerrojo)
        {
            var cuenta = _porUid.Values.First(c => c.Soporte.TarjetaId == tarjetaId);
            if (cuenta.Saldo < monto)
                return Task.FromResult(new ResultadoDebito(false, cuenta.Saldo));

            cuenta.Saldo -= monto;
            _debitos[pasoId] = (cuenta, monto);
            return Task.FromResult(new ResultadoDebito(true, cuenta.Saldo));
        }
    }

    public Task RevertirAsync(long pasoId)
    {
        lock (_cerrojo)
        {
            if (_debitos.Remove(pasoId, out var debito))
                debito.Cuenta.Saldo += debito.Monto;
        }

        return Task.CompletedTask;
    }

    private sealed class Cuenta(SoporteResuelto soporte, decimal saldo)
    {
        public SoporteResuelto Soporte { get; } = soporte;

        public decimal Saldo { get; set; } = saldo;
    }
}
