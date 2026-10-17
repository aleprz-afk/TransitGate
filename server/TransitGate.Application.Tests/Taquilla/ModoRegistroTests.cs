using TransitGate.Application.Cuentas;
using TransitGate.Application.Taquilla;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Taquilla;

public class ModoRegistroTests
{
    private static readonly DateTime Ahora = new(2026, 10, 16, 13, 0, 0, DateTimeKind.Utc);

    private readonly Torniquete _torniquete = new("tq_1", "Torniquete 1");
    private readonly CuentasFalsas _cuentas = new();
    private readonly SolicitudesFalsas _solicitudes = new();
    private readonly IntentosFalsos _intentos = new();
    private readonly NotificadorFalso _notificador = new();
    private readonly RelojFijo _reloj = new(Ahora);
    private readonly ActivarModoRegistro _activar;
    private readonly ValidarLectura _validarLectura;
    private readonly SolicitudPendiente _solicitud;

    public ModoRegistroTests()
    {
        var torniquetes = new TorniquetesFalsos(_torniquete);
        var cerrojo = new CerrojoTorniquetes();
        var unidadDeTrabajo = new UnidadDeTrabajoFalsa();
        var parametros = new ParametrosFijos();

        _activar = new ActivarModoRegistro(
            torniquetes, _solicitudes, unidadDeTrabajo, parametros, cerrojo, Bitacora.Nula(), _reloj);
        _validarLectura = new ValidarLectura(
            torniquetes, _intentos, _cuentas, _cuentas, _solicitudes, unidadDeTrabajo, _notificador,
            parametros, cerrojo, Bitacora.Nula(), new RespuestasDeRegistro(), _reloj);

        _solicitud = _solicitudes.Agregar("1032456789", Ahora.AddHours(-1));
    }

    private Task<ResultadoActivarModoRegistro> Activar(Guid? solicitudId = null) =>
        _activar.EjecutarAsync("tq_1", solicitudId ?? _solicitud.SoporteId);

    private Task<ResultadoLectura?> Leer(string uid, string lecturaId = "a1f3-0001") =>
        _validarLectura.EjecutarAsync("tq_1", lecturaId, uid);

    [Fact] // RF-09 CA1
    public async Task Con_el_torniquete_cerrado_entra_en_modo_registro_para_la_solicitud()
    {
        var resultado = await Activar();

        Assert.Equal(ResultadoActivarModoRegistro.Activado, resultado);
        Assert.Equal(EstadoTorniquete.ModoRegistro, _torniquete.Estado);
        Assert.Equal(_solicitud.SoporteId, _torniquete.SolicitudEnRegistro);
        Assert.Equal(Ahora.AddSeconds(60), _torniquete.ModoRegistroHasta);
    }

    [Fact] // RF-09 CA4
    public async Task Con_un_paso_pendiente_responde_torniquete_ocupado()
    {
        _torniquete.AutorizarPaso(1, Ahora, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        Assert.Equal(ResultadoActivarModoRegistro.TorniqueteOcupado, await Activar());
        Assert.Equal(EstadoTorniquete.EsperandoCruce, _torniquete.Estado);
    }

    [Fact] // RF-09 CA4
    public async Task En_alarma_responde_torniquete_ocupado()
    {
        _torniquete.RegistrarCruce(SentidoCruce.Entrada, Ahora, Ahora);

        Assert.Equal(ResultadoActivarModoRegistro.TorniqueteOcupado, await Activar());
        Assert.Equal(EstadoTorniquete.Alarma, _torniquete.Estado);
    }

    [Fact]
    public async Task Una_solicitud_que_no_esta_pendiente_no_activa_el_modo_registro()
    {
        Assert.Equal(ResultadoActivarModoRegistro.SolicitudNoEncontrada, await Activar(Guid.NewGuid()));
        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
    }

    [Fact] // RF-09 CA2, RF-10 CA1, RF-23 CA3, RF-36 CA4
    public async Task La_lectura_en_modo_registro_liga_el_uid_sin_cobrar_ni_abrir_ni_dejar_intento()
    {
        await Activar();

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Registrada(), resultado);
        Assert.Equal(_solicitud.SoporteId, _solicitudes.UidsLigados["04A1B2C3"]);
        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
        Assert.False(_torniquete.BrazoDeseadoAbierto);
        Assert.Empty(_cuentas.Debitos);
        Assert.Empty(_intentos.Guardados);
        Assert.Empty(await _solicitudes.PendientesAsync(null));
        Assert.Equal(("tq_1", _solicitud.SoporteId, ResultadoLigar.Ligado), Assert.Single(_notificador.Registros));
    }

    [Fact] // RF-10 CA2
    public async Task Un_uid_ya_registrado_no_se_liga_y_el_torniquete_sigue_en_modo_registro()
    {
        _solicitudes.UidsLigados["DEADBEEF"] = Guid.NewGuid();
        await Activar();

        var resultado = await Leer("DEADBEEF");

        Assert.Equal(ResultadoLectura.RegistroDenegado(ResultadoLigar.UidYaRegistrado), resultado);
        Assert.Equal(EstadoTorniquete.ModoRegistro, _torniquete.Estado);
        Assert.Empty(_intentos.Guardados);
        Assert.Equal(ResultadoLigar.UidYaRegistrado, Assert.Single(_notificador.Registros).Resultado);

        // Con otra tarjeta, en el mismo modo registro, sí se liga.
        Assert.Equal(ResultadoLectura.Registrada(), await Leer("04A1B2C3", "a1f3-0002"));
        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
    }

    [Fact] // RF-18 CA4 en modo registro
    public async Task El_reintento_de_una_lectura_registrada_recibe_la_misma_respuesta_y_no_se_valida_como_paso()
    {
        await Activar();
        await Leer("04A1B2C3");

        var reintento = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Registrada(), reintento);
        Assert.Empty(_intentos.Guardados);
        Assert.Single(_notificador.Registros);
    }

    [Fact] // RF-09 CA3, S6
    public async Task Si_vence_el_modo_registro_la_lectura_es_un_paso_normal_y_la_solicitud_sigue_pendiente()
    {
        await Activar();
        _reloj.Ahora = Ahora.AddSeconds(60);

        var resultado = await Leer("04A1B2C3");

        Assert.Equal(ResultadoLectura.Denegar(MotivoIntento.TarjetaNoRegistrada), resultado);
        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
        Assert.Single(await _solicitudes.PendientesAsync(null));
        Assert.Empty(_notificador.Registros);
    }

    [Fact] // RF-36 CA1 y CA2
    public async Task La_taquilla_ve_las_solicitudes_de_la_mas_antigua_a_la_mas_reciente_y_puede_buscar_por_documento()
    {
        var anterior = _solicitudes.Agregar("1121987654", Ahora.AddHours(-2));
        var consultar = new ConsultarSolicitudes(_solicitudes);

        var todas = await consultar.EjecutarAsync(null);
        var buscada = await consultar.EjecutarAsync(" 1032456789 ");
        var ninguna = await consultar.EjecutarAsync("999");

        Assert.Equal([anterior.SoporteId, _solicitud.SoporteId], todas.Select(s => s.SoporteId));
        Assert.Equal(_solicitud.SoporteId, Assert.Single(buscada).SoporteId);
        Assert.Empty(ninguna);
    }
}
