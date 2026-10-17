using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

/// <summary>
/// Implementación de C7 del módulo de cuentas (contratos de integración §7) sobre sus repositorios. Ninguno
/// de sus métodos guarda: participan en la transacción del caso de uso que los llama (RNF-15).
/// </summary>
public sealed class ServicioCuentas(
    ISoporteRepositorio soportes,
    ITarjetaRepositorio tarjetas,
    IClienteRepositorio clientes,
    IDebitoRepositorio debitos,
    IParametrosCuentas parametros,
    CerrojoTarjetas cerrojo,
    TimeProvider reloj) : ISoporteResolver, ISaldoService, ISolicitudesTarjeta
{
    public async Task<SoporteResuelto?> PorUidAsync(string uidHex) =>
        await soportes.PorUidAsync(uidHex) is { } soporte ? await ResolverAsync(soporte) : null;

    public async Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId) =>
        await soportes.ObtenerAsync(soporteId) is { } soporte ? await ResolverAsync(soporte) : null;

    public Task<bool> VerificarFirmaAsync(Guid soporteId, string torniqueteId, byte[] desafio, byte[] firma) =>
        Task.FromResult(false);   // el celular es Fase 4

    public Task<decimal> TarifaVigenteAsync() => Task.FromResult(parametros.Tarifa);

    public async Task<ResultadoDebito> DebitarAsync(Guid tarjetaId, decimal monto, long pasoId)
    {
        var tarjeta = await tarjetas.ObtenerAsync(tarjetaId)
            ?? throw new InvalidOperationException($"No existe la tarjeta {tarjetaId}.");

        using (await cerrojo.TomarAsync(tarjetaId))
        {
            if (!tarjeta.Debitar(monto))
                return new ResultadoDebito(false, tarjeta.Saldo);
        }

        await debitos.AgregarAsync(new DebitoPaso(pasoId, tarjetaId, monto, reloj.GetUtcNow().UtcDateTime));
        return new ResultadoDebito(true, tarjeta.Saldo);
    }

    public async Task RevertirAsync(long pasoId)
    {
        if (await debitos.PorPasoAsync(pasoId) is not { } debito)
            return;

        var tarjeta = await tarjetas.ObtenerAsync(debito.TarjetaId);
        using (await cerrojo.TomarAsync(debito.TarjetaId))
        {
            if (debito.Revertir(reloj.GetUtcNow().UtcDateTime))
                tarjeta!.Acreditar(debito.Monto);
        }
    }

    public async Task<SolicitudPendiente?> PendientePorIdAsync(Guid soporteId) =>
        await soportes.ObtenerAsync(soporteId) is { Estado: EstadoSoporte.Solicitada } soporte
            ? await PendienteAsync(soporte)
            : null;

    public async Task<IReadOnlyList<SolicitudPendiente>> PendientesAsync(string? documento)
    {
        var pendientes = new List<SolicitudPendiente>();
        foreach (var soporte in await soportes.SolicitadosAsync())
        {
            var solicitud = await PendienteAsync(soporte);
            if (documento is null || solicitud.Documento == documento)
                pendientes.Add(solicitud);
        }

        return pendientes;
    }

    public async Task<ResultadoLigar> LigarUidAsync(Guid soporteId, string uidHex)
    {
        if (await soportes.ObtenerAsync(soporteId) is not { Estado: EstadoSoporte.Solicitada } soporte)
            return ResultadoLigar.SolicitudNoPendiente;
        if (await soportes.PorUidAsync(uidHex) is not null)
            return ResultadoLigar.UidYaRegistrado;

        soporte.Entregar(uidHex, reloj.GetUtcNow().UtcDateTime);
        return ResultadoLigar.Ligado;
    }

    private async Task<SoporteResuelto> ResolverAsync(SoporteTarjeta soporte)
    {
        var tarjeta = await tarjetas.ObtenerAsync(soporte.TarjetaId);
        return new SoporteResuelto(soporte.Id, soporte.TarjetaId, tarjeta!.ClienteId, soporte.Estado);
    }

    private async Task<SolicitudPendiente> PendienteAsync(SoporteTarjeta soporte)
    {
        var tarjeta = await tarjetas.ObtenerAsync(soporte.TarjetaId);
        var cliente = await clientes.ObtenerAsync(tarjeta!.ClienteId);
        return new SolicitudPendiente(soporte.Id, cliente!.Id, cliente.NombreCompleto, cliente.NumeroDocumento, soporte.FechaSolicitud);
    }
}
