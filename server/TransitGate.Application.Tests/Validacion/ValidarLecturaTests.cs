using TransitGate.Application.Notificaciones;
using TransitGate.Application.Taquilla;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Validacion;

public class ValidarLecturaTests
{
    // 08:00:00 en Colombia = 13:00:00 UTC (RNF-42).
    private static readonly DateTime Ahora = new(2026, 10, 13, 13, 0, 0, DateTimeKind.Utc);

    private readonly Torniquete _torniquete = new("tq_1", "Torniquete 1");
    private readonly CuentasFalsas _cuentas = new();
    private readonly ParametrosFijos _parametros = new();
    private readonly IntentosFalsos _intentos = new();
    private readonly UnidadDeTrabajoFalsa _unidadDeTrabajo = new();
    private readonly NotificadorFalso _notificador = new();
    private readonly ValidarLectura _validarLectura;

    public ValidarLecturaTests()
    {
        _validarLectura = new ValidarLectura(
            new TorniquetesFalsos(_torniquete),
            _intentos,
            _cuentas,
            _cuentas,
            new SolicitudesFalsas(),
            _unidadDeTrabajo,
            _notificador,
            _parametros,
            new CerrojoTorniquetes(),
            Bitacora.Nula(),
            new RespuestasDeRegistro(),
            new RelojFijo(Ahora));
    }

    private Task<ResultadoLectura?> Leer(string uid, string lecturaId = "a1f3-0001") =>
        _validarLectura.EjecutarAsync("tq_1", lecturaId, uid);

    [Fact] // RF-20 CA1, RF-21 CA1, RF-22 CA1
    public async Task Con_soporte_activo_y_saldo_suficiente_debita_y_abre_un_paso()
    {
        var soporte = _cuentas.Agregar("04A1B2C3", saldo: 5000);

        var resultado = await Leer("04A1B2C3");

        Assert.NotNull(resultado);
        Assert.True(resultado.Autorizado);
        Assert.Equal(1800, resultado.SaldoRestante);
        Assert.Equal(1800, _cuentas.SaldoDe(soporte));
        Assert.Equal(EstadoTorniquete.EsperandoCruce, _torniquete.Estado);
        Assert.Equal(resultado.PasoId, _torniquete.PasoPendiente!.Id);
        Assert.Equal((soporte.TarjetaId, 3200m, resultado.PasoId!.Value), Assert.Single(_cuentas.Debitos));
    }

    [Fact] // RF-16 CA2, RF-39 CA4
    public async Task Al_cliente_le_llegan_el_viaje_y_el_saldo_nuevo_y_un_rechazo_solo_como_viaje()
    {
        var conSaldo = _cuentas.Agregar("04A1B2C3", saldo: 5000);
        var sinSaldo = _cuentas.Agregar("DEADBEEF", saldo: 100);

        await Leer("04A1B2C3", "a1f3-0001");
        _torniquete.RegistrarCruce(SentidoCruce.Entrada, Ahora, Ahora);   // la persona cruza y el torniquete queda libre
        await Leer("DEADBEEF", "a1f3-0002");

        Assert.Equal((conSaldo.ClienteId, 1800m, CausaSaldo.Paso), Assert.Single(_notificador.Saldos));
        Assert.Equal(
            [(conSaldo.ClienteId, ResultadoIntento.Exitoso), (sinSaldo.ClienteId, ResultadoIntento.Rechazado)],
            _notificador.Viajes.Select(v => (v.ClienteId, v.Intento.Resultado)));
        Assert.All(_notificador.Viajes, v => Assert.Equal("Torniquete 1", v.Torniquete));
    }

    [Fact] // RF-20 CA3
    public async Task Con_saldo_igual_a_la_tarifa_autoriza_y_deja_el_saldo_en_cero()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 3200);

        var resultado = await Leer("04A1B2C3");

        Assert.True(resultado!.Autorizado);
        Assert.Equal(0, resultado.SaldoRestante);
    }

    [Fact] // RF-21 CA2
    public async Task Sin_saldo_suficiente_rechaza_y_no_toca_el_saldo_ni_el_torniquete()
    {
        var soporte = _cuentas.Agregar("04A1B2C3", saldo: 2000);

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.SaldoInsuficiente), resultado);
        Assert.Equal(2000, _cuentas.SaldoDe(soporte));
        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
    }

    [Fact] // RF-21 CA3
    public async Task Debita_la_tarifa_vigente_al_momento_del_paso()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000);
        _cuentas.Tarifa = 3500;

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(6500, resultado!.SaldoRestante);
    }

    [Fact] // RF-18 CA2
    public async Task Un_uid_desconocido_se_rechaza_como_tarjeta_no_registrada()
    {
        var resultado = await Leer("0A0B0C0D");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.TarjetaNoRegistrada), resultado);
    }

    [Theory] // RF-20 CA2
    [InlineData(EstadoSoporte.Solicitada)]
    [InlineData(EstadoSoporte.Entregada)]
    public async Task Un_soporte_que_no_esta_activo_se_rechaza(EstadoSoporte estado)
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000, estado);

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.SoporteNoActivo), resultado);
        Assert.Empty(_cuentas.Debitos);
    }

    [Fact] // RF-20 CA6
    public async Task Bloqueado_y_sin_saldo_reporta_solo_el_primer_motivo()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 0, EstadoSoporte.Bloqueada);

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.SoporteBloqueado), resultado);
    }

    [Fact] // RF-20 CA4
    public async Task Con_un_paso_pendiente_rechaza_por_torniquete_ocupado_sin_debitar()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000);
        _cuentas.Agregar("11223344", saldo: 10000);
        await Leer("04A1B2C3", "a1f3-0001");

        var resultado = await Leer("11223344", "a1f3-0002");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.TorniqueteOcupado), resultado);
        Assert.Single(_cuentas.Debitos);
    }

    [Fact] // RF-20 CA5
    public async Task En_alarma_rechaza_por_torniquete_en_alarma()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000);
        _torniquete.RegistrarCruce(SentidoCruce.Entrada, Ahora, Ahora);

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.TorniqueteEnAlarma), resultado);
        Assert.Empty(_cuentas.Debitos);
    }

    [Fact] // RF-18 CA4 y RF-46 CA4
    public async Task Un_reintento_con_el_mismo_lecturaId_responde_lo_mismo_y_no_cobra_otra_vez()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000);

        var primera = await Leer("04A1B2C3", "a1f3-0007");
        var reintento = await Leer("04A1B2C3", "a1f3-0007");

        Assert.Equal(primera, reintento);
        Assert.Single(_cuentas.Debitos);
        Assert.Single(_intentos.Guardados);
        Assert.Equal(1, _unidadDeTrabajo.VecesGuardada);
    }

    [Fact] // RF-23 CA1, RNF-15
    public async Task Un_paso_exitoso_deja_un_intento_exitoso_con_la_tarifa_cobrada()
    {
        var soporte = _cuentas.Agregar("04A1B2C3", saldo: 5000);

        var resultado = await Leer("04A1B2C3", "a1f3-0001");

        var intento = Assert.Single(_intentos.Guardados);
        Assert.Equal(ResultadoIntento.Exitoso, intento.Resultado);
        Assert.Equal(3200, intento.TarifaCobrada);
        Assert.Equal(soporte.TarjetaId, intento.TarjetaId);
        Assert.Equal(resultado!.PasoId, intento.PasoId);
        Assert.Equal("a1f3-0001", intento.LecturaId);
        Assert.Equal(Ahora, intento.Fecha);
        Assert.Equal(TipoSoporte.Fisico, intento.TipoSoporte);
        Assert.Null(intento.Motivo);
        Assert.Equal(1, _unidadDeTrabajo.VecesGuardada);
    }

    [Fact] // RF-32, HU-19 CA1: cada intento llega en vivo al panel; un reintento no se vuelve a avisar
    public async Task Cada_lectura_nueva_avisa_el_intento_al_panel()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 5000);

        await Leer("04A1B2C3", "a1f3-0001");
        await Leer("04A1B2C3", "a1f3-0001");
        await Leer("0A0B0C0D", "a1f3-0002");

        Assert.Equal(_intentos.Guardados, _notificador.Intentos);
    }

    [Fact] // RF-23 CA2
    public async Task Un_uid_desconocido_deja_un_intento_rechazado_sin_tarjeta_y_con_el_uid_leido()
    {
        await Leer("0A0B0C0D");

        var intento = Assert.Single(_intentos.Guardados);
        Assert.Equal(ResultadoIntento.Rechazado, intento.Resultado);
        Assert.Equal(MotivoIntento.TarjetaNoRegistrada, intento.Motivo);
        Assert.Null(intento.TarjetaId);
        Assert.Equal("0A0B0C0D", intento.UidLeido);
        Assert.Null(intento.TarifaCobrada);
    }

    [Fact] // RF-23, RF-24: el rechazo queda en el historial del dueño de la tarjeta
    public async Task Un_rechazo_por_torniquete_ocupado_queda_ligado_a_la_tarjeta()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000);
        var segunda = _cuentas.Agregar("11223344", saldo: 10000);
        await Leer("04A1B2C3", "a1f3-0001");

        await Leer("11223344", "a1f3-0002");

        var rechazo = _intentos.Guardados[1];
        Assert.Equal(MotivoIntento.TorniqueteOcupado, rechazo.Motivo);
        Assert.Equal(segunda.TarjetaId, rechazo.TarjetaId);
    }

    [Fact] // S4 y S13 vienen de la configuración (RNF-40)
    public async Task La_ventana_del_paso_sale_de_los_parametros_de_operacion()
    {
        _cuentas.Agregar("04A1B2C3", saldo: 10000);
        _parametros.VentanaPaso = TimeSpan.FromSeconds(8);

        await Leer("04A1B2C3");

        Assert.Equal(Ahora.AddSeconds(8), _torniquete.PasoPendiente!.FinVentana);
        Assert.Equal(Ahora.AddSeconds(9), _torniquete.PasoPendiente.ExpiraEn);
    }

    [Fact]
    public async Task Un_torniquete_que_no_existe_devuelve_null()
    {
        var resultado = await _validarLectura.EjecutarAsync("tq_9", "a1f3-0001", "04A1B2C3");

        Assert.Null(resultado);
    }
}
