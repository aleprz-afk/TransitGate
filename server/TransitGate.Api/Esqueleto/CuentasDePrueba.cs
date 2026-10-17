using Microsoft.Extensions.Options;
using TransitGate.Application.Cuentas;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Api.Esqueleto;

/// <summary>Tarjetas, solicitudes y tarifa de prueba (sección DatosDePrueba de appsettings.json).</summary>
public sealed class DatosDePrueba
{
    public const string Seccion = "DatosDePrueba";

    public decimal Tarifa { get; set; } = 3200;

    public List<TarjetaDePrueba> Tarjetas { get; set; } = [];

    public List<SolicitudDePrueba> Solicitudes { get; set; } = [];
}

public sealed class TarjetaDePrueba
{
    public string Uid { get; set; } = "";

    public EstadoSoporte Estado { get; set; } = EstadoSoporte.Activa;

    public decimal Saldo { get; set; }
}

/// <summary>Un cliente que pidió la tarjeta física (RF-08) y espera recogerla en taquilla.</summary>
public sealed class SolicitudDePrueba
{
    public string NombreCompleto { get; set; } = "";

    public string Documento { get; set; } = "";

    public decimal Saldo { get; set; }
}

/// <summary>
/// Implementación falsa del contrato C7 mientras no existe el módulo de cuentas (contratos de integración §7).
/// El saldo baja de verdad con cada paso y las solicitudes se ligan en taquilla, pero todo se pierde al reiniciar.
/// </summary>
public sealed class CuentasDePrueba : ISoporteResolver, ISaldoService, ISolicitudesTarjeta
{
    private readonly Lock _cerrojo = new();
    private readonly decimal _tarifa;
    private readonly Dictionary<string, Cuenta> _porUid;
    private readonly List<(SolicitudPendiente Solicitud, Cuenta Cuenta)> _solicitudes;
    private readonly Dictionary<long, (Cuenta Cuenta, decimal Monto)> _debitos = [];

    public CuentasDePrueba(IOptions<DatosDePrueba> opciones, TimeProvider reloj)
    {
        _tarifa = opciones.Value.Tarifa;
        _porUid = opciones.Value.Tarjetas.ToDictionary(
            t => t.Uid.ToUpperInvariant(),
            t => new Cuenta(new SoporteResuelto(Guid.NewGuid(), Guid.NewGuid(), t.Estado), t.Saldo));

        // Fechas escalonadas para que la lista de la taquilla tenga un orden estable (RF-36 CA1).
        var inicio = reloj.GetUtcNow().UtcDateTime.AddMinutes(-opciones.Value.Solicitudes.Count);
        _solicitudes = [.. opciones.Value.Solicitudes.Select((s, i) =>
        {
            var soporte = new SoporteResuelto(Guid.NewGuid(), Guid.NewGuid(), EstadoSoporte.Solicitada);
            var solicitud = new SolicitudPendiente(soporte.SoporteId, Guid.NewGuid(), s.NombreCompleto, s.Documento, inicio.AddMinutes(i));
            return (solicitud, new Cuenta(soporte, s.Saldo));
        })];
    }

    public Task<SoporteResuelto?> PorUidAsync(string uidHex)
    {
        lock (_cerrojo)
            return Task.FromResult(_porUid.GetValueOrDefault(uidHex)?.Soporte);
    }

    public Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId)
    {
        lock (_cerrojo)
            return Task.FromResult(_porUid.Values.FirstOrDefault(c => c.Soporte.SoporteId == soporteId)?.Soporte);
    }

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

    public Task<SolicitudPendiente?> PendientePorIdAsync(Guid soporteId)
    {
        lock (_cerrojo)
            return Task.FromResult<SolicitudPendiente?>(_solicitudes.FirstOrDefault(s => s.Solicitud.SoporteId == soporteId).Solicitud);
    }

    public Task<IReadOnlyList<SolicitudPendiente>> PendientesAsync(string? documento)
    {
        lock (_cerrojo)
            return Task.FromResult<IReadOnlyList<SolicitudPendiente>>(
                [.. _solicitudes.Select(s => s.Solicitud)
                    .Where(s => documento is null || s.Documento == documento)
                    .OrderBy(s => s.Fecha)]);
    }

    public Task<ResultadoLigar> LigarUidAsync(Guid soporteId, string uidHex)
    {
        lock (_cerrojo)
        {
            var indice = _solicitudes.FindIndex(s => s.Solicitud.SoporteId == soporteId);
            if (indice < 0)
                return Task.FromResult(ResultadoLigar.SolicitudNoPendiente);
            if (_porUid.ContainsKey(uidHex))
                return Task.FromResult(ResultadoLigar.UidYaRegistrado);

            // Solicitada → Entregada (RF-10 CA1). Queda sin activar hasta que el cliente confirme (RF-11).
            var cuenta = _solicitudes[indice].Cuenta;
            _solicitudes.RemoveAt(indice);
            cuenta.Soporte = cuenta.Soporte with { Estado = EstadoSoporte.Entregada };
            _porUid[uidHex] = cuenta;
            return Task.FromResult(ResultadoLigar.Ligado);
        }
    }

    private sealed class Cuenta(SoporteResuelto soporte, decimal saldo)
    {
        public SoporteResuelto Soporte { get; set; } = soporte;

        public decimal Saldo { get; set; } = saldo;
    }
}
