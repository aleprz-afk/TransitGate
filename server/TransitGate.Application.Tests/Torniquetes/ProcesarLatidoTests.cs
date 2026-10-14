using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Torniquetes;

public class ProcesarLatidoTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 13, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Ventana = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Gracia = TimeSpan.FromSeconds(1);

    private readonly Torniquete _torniquete = new("tq_1", "Torniquete 1");
    private readonly RelojFijo _reloj = new(T0);
    private readonly ProcesarLatido _procesarLatido;
    private readonly ConsultarTorniquetes _consultarTorniquetes;

    public ProcesarLatidoTests()
    {
        var repositorio = new TorniquetesFalsos(_torniquete);
        var cerrojo = new CerrojoTorniquetes();
        _procesarLatido = new ProcesarLatido(repositorio, cerrojo, Bitacora.Nula(), _reloj);
        _consultarTorniquetes = new ConsultarTorniquetes(repositorio, cerrojo, new ParametrosFijos(), Bitacora.Nula(), _reloj);
    }

    private Task<OrdenEsp32?> Latido(long? pasoIdAbierto = null, string arranque = "a1f3") =>
        _procesarLatido.DelEsp32Async("tq_1", new EstadoDeseado(pasoIdAbierto is not null, false), pasoIdAbierto, arranque);

    [Fact] // RF-45
    public async Task Con_un_paso_pendiente_el_latido_ordena_el_brazo_abierto()
    {
        _torniquete.AutorizarPaso(7, T0, Ventana, Gracia);

        var orden = await Latido();

        Assert.Equal(new OrdenEsp32(new EstadoDeseado(true, false), PasoId: 7, ModoRegistro: false), orden);
    }

    [Fact] // RF-29 CA1
    public async Task Si_el_paso_vencio_el_latido_ordena_cerrar()
    {
        _torniquete.AutorizarPaso(7, T0, Ventana, Gracia);
        _reloj.Ahora = T0.AddSeconds(11);

        var orden = await Latido();

        Assert.Equal(new OrdenEsp32(new EstadoDeseado(false, false), PasoId: null, ModoRegistro: false), orden);
    }

    [Fact] // RF-45 CA3
    public async Task Tras_un_reinicio_del_esp32_en_alarma_recibe_brazo_cerrado_y_sirena_encendida()
    {
        _torniquete.RegistrarCruce(SentidoCruce.Entrada, T0, T0);
        await Latido(arranque: "a1f3");

        var orden = await Latido(arranque: "b702");

        Assert.Equal(new EstadoDeseado(BrazoAbierto: false, SirenaEncendida: true), orden!.Deseado);
        Assert.Equal("b702", _torniquete.ArranqueEsp32);
    }

    [Fact] // RF-22 CA4
    public async Task El_latido_confirma_la_apertura_del_paso()
    {
        var paso = _torniquete.AutorizarPaso(7, T0, Ventana, Gracia);
        _reloj.Ahora = T0.AddMilliseconds(300);

        await Latido(pasoIdAbierto: 7);

        Assert.Equal(_reloj.Ahora, paso.AperturaConfirmadaEn);
    }

    [Fact]
    public async Task Un_torniquete_que_no_existe_devuelve_null()
    {
        Assert.Null(await _procesarLatido.DelEsp32Async("tq_9", default, null, "a1f3"));
        Assert.False(await _procesarLatido.DeLaVisionAsync("tq_9"));
    }

    [Fact] // S14, RF-37
    public async Task La_consulta_muestra_el_torniquete_sin_conexion_tras_5_s_sin_latidos()
    {
        await Latido();

        _reloj.Ahora = T0.AddSeconds(4);
        Assert.True(Assert.Single(await _consultarTorniquetes.EjecutarAsync()).Conectado);

        _reloj.Ahora = T0.AddSeconds(5);
        var resumen = Assert.Single(await _consultarTorniquetes.EjecutarAsync());
        Assert.False(resumen.Conectado);
        Assert.Equal(T0, resumen.UltimaSenal);
    }

    [Fact] // RF-29 CA1, RF-31
    public async Task La_consulta_no_muestra_un_paso_que_ya_vencio()
    {
        _torniquete.AutorizarPaso(7, T0, Ventana, Gracia);
        _reloj.Ahora = T0.AddSeconds(12);

        var resumen = Assert.Single(await _consultarTorniquetes.EjecutarAsync());

        Assert.Equal(EstadoTorniquete.Cerrado, resumen.Estado);
    }
}
