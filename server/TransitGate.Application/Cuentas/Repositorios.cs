using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

// Puertos de persistencia del módulo de cuentas. Como los del torniquete, solo agregan y buscan: los cambios
// se guardan con IUnidadDeTrabajo.GuardarCambiosAsync al final del caso de uso (RNF-15).

public interface IClienteRepositorio
{
    Task AgregarAsync(Cliente cliente, CancellationToken ct = default);

    Task<Cliente?> ObtenerAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>El correo se compara en minúsculas (RF-01 CA2).</summary>
    Task<bool> ExisteCorreoAsync(string correo, CancellationToken ct = default);

    Task<bool> ExisteDocumentoAsync(string numeroDocumento, CancellationToken ct = default);
}

public interface ITarjetaRepositorio
{
    Task AgregarAsync(Tarjeta tarjeta, CancellationToken ct = default);

    /// <summary>
    /// Para cambiar el saldo: se pide con el CerrojoTarjetas tomado, así se lee el saldo vigente y no uno
    /// que otra petición ya cambió (RNF-16).
    /// </summary>
    Task<Tarjeta?> ObtenerAsync(Guid tarjetaId, CancellationToken ct = default);

    /// <summary>Solo para leer: un cliente tiene exactamente una (RF-07). Para cambiar el saldo, ObtenerAsync.</summary>
    Task<Tarjeta?> DelClienteAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>El cliente dueño de la tarjeta, sin cargarla.</summary>
    Task<Guid?> ClienteDeAsync(Guid tarjetaId, CancellationToken ct = default);
}

public interface ISoporteRepositorio
{
    Task AgregarAsync(SoporteTarjeta soporte, CancellationToken ct = default);

    Task<SoporteTarjeta?> ObtenerAsync(Guid soporteId, CancellationToken ct = default);

    /// <summary>Cualquier soporte con ese UID, en cualquier estado (RF-10 CA2).</summary>
    Task<SoporteTarjeta?> PorUidAsync(string uid, CancellationToken ct = default);

    Task<IReadOnlyList<SoporteTarjeta>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default);

    /// <summary>Los físicos en Solicitada, el más antiguo primero (RF-36 CA1).</summary>
    Task<IReadOnlyList<SoporteTarjeta>> SolicitadosAsync(CancellationToken ct = default);
}

public interface IRecargaRepositorio
{
    Task<long> SiguienteIdAsync(CancellationToken ct = default);

    Task AgregarAsync(Recarga recarga, CancellationToken ct = default);

    /// <summary>Para no aplicar dos veces la misma recarga (RF-15 CA5).</summary>
    Task<Recarga?> PorClaveAsync(Guid tarjetaId, string claveIdempotencia, CancellationToken ct = default);

    /// <summary>Las más recientes primero (RF-17 CA1).</summary>
    Task<IReadOnlyList<Recarga>> DeLaTarjetaAsync(Guid tarjetaId, CancellationToken ct = default);
}

public interface IDebitoRepositorio
{
    Task AgregarAsync(DebitoPaso debito, CancellationToken ct = default);

    Task<DebitoPaso?> PorPasoAsync(long pasoId, CancellationToken ct = default);
}
