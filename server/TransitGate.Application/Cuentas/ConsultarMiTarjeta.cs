using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

public sealed record SoporteVista(TipoSoporte Tipo, EstadoSoporte Estado);

/// <summary>Soporte es null si el cliente todavía no pidió ninguno.</summary>
public sealed record MiTarjeta(Guid TarjetaId, decimal Saldo, SoporteVista? Soporte);

/// <summary>
/// Saldo y estado de la tarjeta del cliente (RF-16 CA1). Se muestra el soporte vigente más reciente; si todos
/// están bloqueados, el último bloqueado, para que el portal ofrezca pedir otra.
/// </summary>
public sealed class ConsultarMiTarjeta(ITarjetaRepositorio tarjetas, ISoporteRepositorio soportes)
{
    public async Task<Resultado<MiTarjeta>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        if (await tarjetas.DelClienteAsync(clienteId, ct) is not { } tarjeta)
            return ErrorCuenta.TarjetaNoEncontrada;

        var soporte = (await soportes.DeLaTarjetaAsync(tarjeta.Id, ct))
            .OrderByDescending(s => s.Vigente)
            .ThenByDescending(s => s.FechaSolicitud)
            .FirstOrDefault();

        return new MiTarjeta(tarjeta.Id, tarjeta.Saldo, soporte is null ? null : new SoporteVista(soporte.Tipo, soporte.Estado));
    }
}
