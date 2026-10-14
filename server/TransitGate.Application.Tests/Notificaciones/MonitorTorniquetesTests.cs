using TransitGate.Application.Notificaciones;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Notificaciones;

public class MonitorTorniquetesTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 13, 0, 0, DateTimeKind.Utc);
    private static readonly EstadoDeseado Cerrado = new(false, false);

    private readonly Torniquete _torniquete = new("tq_1", "Torniquete 1");
    private readonly RelojFijo _reloj = new(T0);
    private readonly NotificadorFalso _notificador = new();
    private readonly MonitorTorniquetes _monitor;

    public MonitorTorniquetesTests()
    {
        _monitor = new MonitorTorniquetes(
            new TorniquetesFalsos(_torniquete),
            new CerrojoTorniquetes(),
            new ParametrosFijos(),
            _notificador,
            Bitacora.Nula(),
            _reloj);
    }

    private void Avanzar(double segundos) => _reloj.Ahora = _reloj.Ahora.AddSeconds(segundos);

    [Fact]
    public async Task La_primera_revision_publica_el_estado_y_las_siguientes_solo_si_cambia()
    {
        await _monitor.RevisarAsync();
        await _monitor.RevisarAsync();

        var publicado = Assert.Single(_notificador.Enviados);
        Assert.Equal(new EstadoTorniqueteVista("tq_1", EstadoTorniquete.Cerrado, false, false, false), publicado);
        Assert.Equal(publicado, Assert.Single(_monitor.UltimosPublicados));
    }

    [Fact] // RF-37, S14
    public async Task Publica_cuando_el_esp32_se_conecta_y_cuando_deja_de_enviar_latidos()
    {
        _torniquete.RegistrarLatidoEsp32(T0, Cerrado, null, "a1f3");
        await _monitor.RevisarAsync();

        Avanzar(5);
        await _monitor.RevisarAsync();

        Assert.Equal([true, false], _notificador.Enviados.Select(e => e.Esp32Conectado));
    }

    [Fact] // RF-30, S14
    public async Task Publica_cuando_la_vision_queda_fuera_de_linea()
    {
        _torniquete.RegistrarLatidoVision(T0);
        await _monitor.RevisarAsync();

        Avanzar(15);
        await _monitor.RevisarAsync();

        Assert.Equal([true, false], _notificador.Enviados.Select(e => e.VisionConectada));
    }

    [Fact] // RF-29 CA1: el paso vence aunque no llegue ningún latido
    public async Task Cierra_el_paso_vencido_sin_esperar_un_latido()
    {
        _torniquete.AutorizarPaso(7, T0, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        await _monitor.RevisarAsync();

        Avanzar(11);
        await _monitor.RevisarAsync();

        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
        Assert.Equal(
            [EstadoTorniquete.EsperandoCruce, EstadoTorniquete.Cerrado],
            _notificador.Enviados.Select(e => e.Estado));
    }

    [Fact] // RF-45 CA4
    public async Task Publica_la_desincronizacion_si_el_esp32_no_aplica_el_estado_deseado()
    {
        _torniquete.RegistrarCruce(SentidoCruce.Entrada, T0, T0);   // Alarma: sirena encendida
        _torniquete.RegistrarLatidoEsp32(T0, Cerrado, null, "a1f3");  // pero reporta la sirena apagada
        await _monitor.RevisarAsync();

        Avanzar(2.5);
        _torniquete.RegistrarLatidoEsp32(_reloj.Ahora, Cerrado, null, "a1f3");
        await _monitor.RevisarAsync();

        Assert.Equal([false, true], _notificador.Enviados.Select(e => e.Desincronizado));
    }
}
