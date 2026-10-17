using TransitGate.Application.Accesos;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

/// <summary>Las recargas del cliente, la más reciente primero, aprobadas y rechazadas (RF-17).</summary>
public sealed class ConsultarRecargas(ITarjetaRepositorio tarjetas, IRecargaRepositorio recargas)
{
    public async Task<Resultado<IReadOnlyList<Recarga>>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        if (await tarjetas.DelClienteAsync(clienteId, ct) is not { } tarjeta)
            return ErrorCuenta.TarjetaNoEncontrada;

        return new Resultado<IReadOnlyList<Recarga>>(await recargas.DeLaTarjetaAsync(tarjeta.Id, ct), null);
    }
}

public sealed record Viaje(IntentoAcceso Intento, string TorniqueteNombre);

/// <summary>
/// Historial de viajes del cliente (RF-39): los intentos con su tarjeta, exitosos y rechazados, el más
/// reciente primero. Como van por tarjeta y no por soporte, siguen ahí después de un bloqueo o reemplazo (CA3).
/// En el MVP es una lista simple de los últimos 100, sin filtros (RF-40 va después).
/// </summary>
public sealed class ConsultarViajes(
    ITarjetaRepositorio tarjetas,
    IIntentoRepositorio intentos,
    ITorniqueteRepositorio torniquetes)
{
    public const int Limite = 100;

    public async Task<Resultado<IReadOnlyList<Viaje>>> EjecutarAsync(Guid clienteId, CancellationToken ct = default)
    {
        if (await tarjetas.DelClienteAsync(clienteId, ct) is not { } tarjeta)
            return ErrorCuenta.TarjetaNoEncontrada;

        var nombres = new Dictionary<string, string>();
        var viajes = new List<Viaje>();
        foreach (var intento in await intentos.ListarPorTarjetaAsync(tarjeta.Id, Limite, ct))
        {
            if (!nombres.TryGetValue(intento.TorniqueteId, out var nombre))
            {
                nombre = (await torniquetes.ObtenerAsync(intento.TorniqueteId, ct))?.Nombre ?? intento.TorniqueteId;
                nombres[intento.TorniqueteId] = nombre;
            }

            viajes.Add(new Viaje(intento, nombre));
        }

        return new Resultado<IReadOnlyList<Viaje>>(viajes, null);
    }
}
