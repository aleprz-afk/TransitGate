using TransitGate.Application.Alertas;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Alertas;

public class ApagarAlarmaTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 13, 0, 0, DateTimeKind.Utc);

    private readonly Torniquete _torniquete = new("tq_1", "Torniquete 1");
    private readonly AlertasFalsas _alertas = new();
    private readonly NotificadorFalso _notificador = new();
    private readonly RelojFijo _reloj = new(T0);
    private readonly ApagarAlarma _apagarAlarma;
    private readonly AlertaSeguridad _alerta;

    public ApagarAlarmaTests()
    {
        _apagarAlarma = new ApagarAlarma(
            _alertas, new TorniquetesFalsos(_torniquete), new UnidadDeTrabajoFalsa(),
            _notificador, new CerrojoTorniquetes(), Bitacora.Nula(), _reloj);

        // Un colado: el torniquete queda en Alarma con su alerta abierta.
        _torniquete.RegistrarCruce(SentidoCruce.Entrada, T0, T0);
        _alerta = new AlertaSeguridad(1, "tq_1", T0);
        _alertas.Guardadas.Add(_alerta);
    }

    [Fact] // RF-34, HU-22 CA1
    public async Task Apagar_la_alarma_cierra_el_torniquete_y_deja_la_alerta_atendida()
    {
        _reloj.Ahora = T0.AddSeconds(30);

        Assert.True(await _apagarAlarma.EjecutarAsync(1));

        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
        Assert.False(_torniquete.SirenaDeseada);
        Assert.Equal(EstadoAlerta.Atendida, _alerta.Estado);
        Assert.Equal(T0.AddSeconds(30), _alerta.AtendidaEn);
        Assert.Equal([_alerta], _notificador.AlertasActualizadas);
    }

    [Fact]
    public async Task Apagar_una_alarma_ya_apagada_no_hace_nada()
    {
        await _apagarAlarma.EjecutarAsync(1);

        Assert.True(await _apagarAlarma.EjecutarAsync(1));

        Assert.Single(_notificador.AlertasActualizadas);
    }

    [Fact]
    public async Task Una_alerta_que_no_existe_devuelve_false()
    {
        Assert.False(await _apagarAlarma.EjecutarAsync(99));
        Assert.Equal(EstadoTorniquete.Alarma, _torniquete.Estado);
    }
}
