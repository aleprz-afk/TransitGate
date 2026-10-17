using TransitGate.Application.Cuentas;
using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Tests.Cuentas;

public class SaldoTests
{
    private readonly ModuloCuentasFalso _cuentas = new();

    private Recargar Recargar() => new(
        _cuentas.Tarjetas, _cuentas.Recargas, _cuentas.Parametros, _cuentas.UnidadDeTrabajo,
        _cuentas.Notificador, _cuentas.Cerrojo, _cuentas.Reloj);

    [Fact] // RF-15 CA1, RF-16 CA3
    public async Task Una_recarga_aprobada_suma_al_saldo_y_avisa_al_cliente()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente(saldo: 1800);

        var resultado = await Recargar().EjecutarAsync(cliente.Id, 10000, "clave-1", simularRechazo: false);

        var recarga = resultado.Valor!;
        Assert.True(recarga.Aprobada);
        Assert.Equal((10000m, 11800m), (recarga.Monto, recarga.SaldoResultante));
        Assert.Equal(11800, tarjeta.Saldo);
        Assert.Equal((cliente.Id, 11800m, CausaSaldo.Recarga), Assert.Single(_cuentas.Notificador.Saldos));
    }

    [Fact] // RF-15 CA2
    public async Task Un_pago_rechazado_deja_la_recarga_rechazada_y_el_saldo_igual()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente(saldo: 1800);

        var resultado = await Recargar().EjecutarAsync(cliente.Id, 10000, null, simularRechazo: true);

        Assert.False(resultado.Valor!.Aprobada);
        Assert.Equal(1800, resultado.Valor.SaldoResultante);
        Assert.Equal(1800, tarjeta.Saldo);
        Assert.Single(_cuentas.Recargas.Guardadas);
        Assert.Empty(_cuentas.Notificador.Saldos);
    }

    [Theory] // RF-15 CA3, S8
    [InlineData(999)]
    [InlineData(200001)]
    public async Task Un_monto_fuera_de_rango_no_se_procesa(int monto)
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente();

        var resultado = await Recargar().EjecutarAsync(cliente.Id, monto, null, simularRechazo: false);

        Assert.Equal("MontoFueraDeRango", resultado.Error!.Codigo);
        Assert.Contains("1.000", resultado.Error.Mensaje.Replace(",", "."));
        Assert.Empty(_cuentas.Recargas.Guardadas);
        Assert.Equal(0, tarjeta.Saldo);
    }

    [Theory] // RF-15 CA3: los extremos sí se aceptan
    [InlineData(1000)]
    [InlineData(200000)]
    public async Task Los_extremos_del_rango_si_se_aceptan(int monto)
    {
        var (cliente, _, _) = _cuentas.Cliente();

        Assert.True((await Recargar().EjecutarAsync(cliente.Id, monto, null, simularRechazo: false)).Exito);
    }

    [Fact] // RF-15 CA4
    public async Task Con_el_soporte_bloqueado_la_recarga_se_aplica_igual()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente(estadoFisico: EstadoSoporte.Bloqueada);

        await Recargar().EjecutarAsync(cliente.Id, 5000, null, simularRechazo: false);

        Assert.Equal(5000, tarjeta.Saldo);
    }

    [Fact] // RF-15 CA5
    public async Task La_misma_recarga_enviada_dos_veces_suma_una_sola_vez()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente();

        var resultados = await Task.WhenAll(
            Recargar().EjecutarAsync(cliente.Id, 10000, "doble-clic", simularRechazo: false),
            Recargar().EjecutarAsync(cliente.Id, 10000, "doble-clic", simularRechazo: false));

        Assert.Equal(10000, tarjeta.Saldo);
        Assert.Single(_cuentas.Recargas.Guardadas);
        Assert.Equal(resultados[0].Valor!.Id, resultados[1].Valor!.Id);
    }

    [Fact] // RNF-16
    public async Task Veinte_recargas_en_paralelo_suman_exacto()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente();

        await Task.WhenAll(Enumerable.Range(1, 20).Select(i =>
            Task.Run(() => Recargar().EjecutarAsync(cliente.Id, 1000, $"r-{i}", simularRechazo: false))));

        Assert.Equal(20000, tarjeta.Saldo);
        Assert.Equal(20, _cuentas.Recargas.Guardadas.Count);
    }

    [Fact] // RF-17 CA1 y CA2
    public async Task El_historial_de_recargas_va_de_la_mas_reciente_a_la_mas_antigua()
    {
        var (cliente, _, _) = _cuentas.Cliente();
        var consultar = new ConsultarRecargas(_cuentas.Tarjetas, _cuentas.Recargas);
        Assert.Empty((await consultar.EjecutarAsync(cliente.Id)).Valor!);

        await Recargar().EjecutarAsync(cliente.Id, 1000, null, simularRechazo: false);
        await Recargar().EjecutarAsync(cliente.Id, 2000, null, simularRechazo: true);
        await Recargar().EjecutarAsync(cliente.Id, 3000, null, simularRechazo: false);

        var historial = (await consultar.EjecutarAsync(cliente.Id)).Valor!;
        Assert.Equal([(3000m, true, 4000m), (2000m, false, 1000m), (1000m, true, 1000m)],
            historial.Select(r => (r.Monto, r.Aprobada, r.SaldoResultante)));
    }

    [Fact] // S3, C7
    public async Task Debitar_sin_saldo_suficiente_no_descuenta_nada()
    {
        var (_, tarjeta, _) = _cuentas.Cliente(saldo: 2000);

        var debito = await _cuentas.Servicio().DebitarAsync(tarjeta.Id, 3200, pasoId: 1);

        Assert.Equal(new ResultadoDebito(false, 2000), debito);
        Assert.Empty(_cuentas.Debitos.Guardados);
    }

    [Fact] // RF-46
    public async Task Revertir_un_paso_devuelve_la_tarifa_una_sola_vez()
    {
        var (_, tarjeta, _) = _cuentas.Cliente(saldo: 5000);
        var servicio = _cuentas.Servicio();
        Assert.Equal(new ResultadoDebito(true, 1800), await servicio.DebitarAsync(tarjeta.Id, 3200, pasoId: 9));

        await servicio.RevertirAsync(9);
        await servicio.RevertirAsync(9);
        await servicio.RevertirAsync(123);   // un paso sin débito no hace nada

        Assert.Equal(5000, tarjeta.Saldo);
    }

    [Fact] // C7: el resolver entrega el cliente para avisarle de su paso
    public async Task El_resolver_identifica_el_soporte_su_tarjeta_y_su_cliente()
    {
        var (cliente, tarjeta, soporte) = _cuentas.Cliente(estadoFisico: EstadoSoporte.Activa, uid: "DEADBEEF");

        var resuelto = await _cuentas.Servicio().PorUidAsync("DEADBEEF");

        Assert.Equal(new SoporteResuelto(soporte!.Id, tarjeta.Id, cliente.Id, EstadoSoporte.Activa), resuelto);
        Assert.Null(await _cuentas.Servicio().PorUidAsync("0BADCAFE"));
    }

    [Fact] // RF-10 CA1 y CA2 sobre el módulo real
    public async Task Ligar_un_uid_entrega_la_solicitud_y_un_uid_de_otro_soporte_no_se_liga()
    {
        _cuentas.Cliente(estadoFisico: EstadoSoporte.Bloqueada, uid: "0BADCAFE", documento: "111111111");
        var (_, _, solicitud) = _cuentas.Cliente(estadoFisico: EstadoSoporte.Solicitada);
        var servicio = _cuentas.Servicio();

        Assert.Equal(ResultadoLigar.UidYaRegistrado, await servicio.LigarUidAsync(solicitud!.Id, "0BADCAFE"));
        Assert.Equal(EstadoSoporte.Solicitada, solicitud.Estado);

        Assert.Equal(ResultadoLigar.Ligado, await servicio.LigarUidAsync(solicitud.Id, "A1B2C3D4"));
        Assert.Equal((EstadoSoporte.Entregada, "A1B2C3D4"), (solicitud.Estado, solicitud.Uid));
        Assert.Empty(await servicio.PendientesAsync(null));

        Assert.Equal(ResultadoLigar.SolicitudNoPendiente, await servicio.LigarUidAsync(solicitud.Id, "11223344"));
    }
}
