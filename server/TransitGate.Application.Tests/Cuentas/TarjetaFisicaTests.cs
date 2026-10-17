using TransitGate.Application.Cuentas;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Tests.Cuentas;

public class TarjetaFisicaTests
{
    private readonly ModuloCuentasFalso _cuentas = new();

    private SolicitarTarjetaFisica Solicitar() => new(
        _cuentas.Tarjetas, _cuentas.Soportes, _cuentas.Servicio(), _cuentas.UnidadDeTrabajo, _cuentas.Notificador, _cuentas.Reloj);

    private ConfirmarRecepcion Confirmar() => new(_cuentas.Tarjetas, _cuentas.Soportes, _cuentas.UnidadDeTrabajo, _cuentas.Reloj);

    [Fact] // RF-08 CA1
    public async Task Solicitar_crea_un_soporte_fisico_sin_uid_que_aparece_en_la_taquilla()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente();

        var resultado = await Solicitar().EjecutarAsync(cliente.Id);

        var soporte = Assert.Single(_cuentas.Soportes.Guardados);
        Assert.Equal(new SolicitudCreada(soporte.Id, EstadoSoporte.Solicitada), resultado.Valor);
        Assert.Equal((TipoSoporte.Fisico, tarjeta.Id, (string?)null), (soporte.Tipo, soporte.TarjetaId, soporte.Uid));

        var pendiente = Assert.Single(await _cuentas.Servicio().PendientesAsync(null));
        Assert.Equal((soporte.Id, cliente.Id, "Luisa Rojas", "1032456789"),
            (pendiente.SoporteId, pendiente.ClienteId, pendiente.NombreCompleto, pendiente.Documento));
        Assert.Equal(pendiente, Assert.Single(_cuentas.Notificador.SolicitudesNuevas));
    }

    [Theory] // RF-08 CA2
    [InlineData(EstadoSoporte.Solicitada)]
    [InlineData(EstadoSoporte.Entregada)]
    [InlineData(EstadoSoporte.Activa)]
    public async Task Con_una_fisica_vigente_no_se_puede_pedir_otra(EstadoSoporte estado)
    {
        var (cliente, _, _) = _cuentas.Cliente(estadoFisico: estado);

        var resultado = await Solicitar().EjecutarAsync(cliente.Id);

        Assert.Equal(("TarjetaFisicaExistente", TipoError.Conflicto), (resultado.Error!.Codigo, resultado.Error.Tipo));
        Assert.Single(_cuentas.Soportes.Guardados);
    }

    [Fact] // RF-08 CA3
    public async Task Con_la_fisica_bloqueada_se_pide_una_reposicion_sin_tocar_el_saldo()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente(saldo: 8000, estadoFisico: EstadoSoporte.Bloqueada);

        var resultado = await Solicitar().EjecutarAsync(cliente.Id);

        Assert.True(resultado.Exito);
        Assert.Equal(2, _cuentas.Soportes.Guardados.Count);
        Assert.Equal(8000, tarjeta.Saldo);
    }

    [Fact] // RF-11 CA1, RF-10 CA3
    public async Task Confirmar_la_recepcion_activa_la_tarjeta_y_ese_uid_ya_valida_pasos()
    {
        var (cliente, _, soporte) = _cuentas.Cliente(estadoFisico: EstadoSoporte.Entregada);
        Assert.Equal(EstadoSoporte.Entregada, (await _cuentas.Servicio().PorUidAsync("04A1B2C3"))!.Estado);

        var resultado = await Confirmar().EjecutarAsync(cliente.Id);

        Assert.Equal(EstadoSoporte.Activa, resultado.Valor);
        Assert.Equal(EstadoSoporte.Activa, soporte!.Estado);
        Assert.Equal(EstadoSoporte.Activa, (await _cuentas.Servicio().PorUidAsync("04A1B2C3"))!.Estado);
    }

    [Theory] // RF-11 CA3
    [InlineData(EstadoSoporte.Solicitada)]
    [InlineData(EstadoSoporte.Activa)]
    [InlineData(EstadoSoporte.Bloqueada)]
    public async Task Solo_se_confirma_una_tarjeta_entregada(EstadoSoporte estado)
    {
        var (cliente, _, soporte) = _cuentas.Cliente(estadoFisico: estado);

        var resultado = await Confirmar().EjecutarAsync(cliente.Id);

        Assert.Equal("EstadoInvalido", resultado.Error!.Codigo);
        Assert.Equal(estado, soporte!.Estado);
    }

    [Fact] // RF-16 CA1
    public async Task Mi_tarjeta_muestra_el_saldo_y_el_soporte_vigente()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente(saldo: 6800, estadoFisico: EstadoSoporte.Bloqueada);
        _cuentas.Reloj.Ahora = ModuloCuentasFalso.Ahora.AddDays(1);
        await Solicitar().EjecutarAsync(cliente.Id);

        var miTarjeta = await new ConsultarMiTarjeta(_cuentas.Tarjetas, _cuentas.Soportes).EjecutarAsync(cliente.Id);

        Assert.Equal(new MiTarjeta(tarjeta.Id, 6800, new SoporteVista(TipoSoporte.Fisico, EstadoSoporte.Solicitada)), miTarjeta.Valor);
    }

    [Fact]
    public async Task Sin_soportes_mi_tarjeta_muestra_soporte_nulo()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente();

        var miTarjeta = await new ConsultarMiTarjeta(_cuentas.Tarjetas, _cuentas.Soportes).EjecutarAsync(cliente.Id);

        Assert.Equal(new MiTarjeta(tarjeta.Id, 0, null), miTarjeta.Valor);
    }

    [Fact] // RF-06 CA1: un cliente sin tarjeta, o un id ajeno, recibe 404
    public async Task Un_cliente_que_no_existe_recibe_tarjeta_no_encontrada()
    {
        var resultado = await new ConsultarMiTarjeta(_cuentas.Tarjetas, _cuentas.Soportes).EjecutarAsync(Guid.NewGuid());

        Assert.Equal(TipoError.NoEncontrado, resultado.Error!.Tipo);
    }
}
