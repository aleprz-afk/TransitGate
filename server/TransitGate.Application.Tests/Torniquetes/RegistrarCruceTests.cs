using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Torniquetes;

public class RegistrarCruceTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 13, 0, 0, DateTimeKind.Utc);

    private readonly Torniquete _torniquete = new("tq_1", "Torniquete 1");
    private readonly CrucesFalsos _cruces = new();
    private readonly AlertasFalsas _alertas = new();
    private readonly IntentosFalsos _intentos = new();
    private readonly UnidadDeTrabajoFalsa _unidadDeTrabajo = new();
    private readonly NotificadorFalso _notificador = new();
    private readonly RelojFijo _reloj = new(T0);
    private readonly RegistrarCruce _registrarCruce;

    public RegistrarCruceTests()
    {
        _registrarCruce = new RegistrarCruce(
            new TorniquetesFalsos(_torniquete), _cruces, _alertas, _intentos, _unidadDeTrabajo,
            _notificador, new CerrojoTorniquetes(), Bitacora.Nula(), _reloj);
    }

    private Task<ResultadoRegistroCruce?> Cruzar(long trackId, SentidoCruce sentido = SentidoCruce.Entrada) =>
        _registrarCruce.EjecutarAsync("tq_1", "v-1", trackId, sentido, _reloj.Ahora);

    [Fact] // RF-26 CA1, RF-27 CA2
    public async Task Un_cruce_con_paso_pendiente_queda_registrado_y_ligado_al_paso()
    {
        _torniquete.AutorizarPaso(7, T0, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        _reloj.Ahora = T0.AddSeconds(2);

        var registro = await Cruzar(trackId: 1);

        var cruce = Assert.Single(_cruces.Guardados);
        Assert.Same(cruce, registro!.Cruce);
        Assert.Equal(TipoResultadoCruce.Consumido, cruce.Resultado);
        Assert.Equal(7, cruce.PasoId);
        Assert.Equal(T0.AddSeconds(2), cruce.HoraFotograma);
        Assert.Empty(_intentos.Guardados);
        Assert.Empty(_alertas.Guardadas);
        Assert.Equal(1, _unidadDeTrabajo.VecesGuardada);
    }

    [Fact] // RF-28 CA1
    public async Task Un_cruce_sin_validacion_abre_una_alerta_y_deja_un_intento_de_fraude()
    {
        var registro = await Cruzar(trackId: 1);

        var alerta = Assert.Single(_alertas.Guardadas);
        Assert.Equal(EstadoAlerta.Abierta, alerta.Estado);
        Assert.Equal(T0, alerta.Fecha);

        var fraude = Assert.Single(_intentos.Guardados);
        Assert.Equal(ResultadoIntento.Fraude, fraude.Resultado);
        Assert.Equal(MotivoIntento.FraudeSinValidacion, fraude.Motivo);
        Assert.Equal(alerta.Id, fraude.AlertaId);

        Assert.Equal(TipoResultadoCruce.Fraude, registro!.Cruce!.Resultado);
        Assert.Equal(alerta.Id, registro.Cruce.AlertaId);
        Assert.Equal(EstadoTorniquete.Alarma, _torniquete.Estado);

        Assert.Equal([alerta], _notificador.AlertasNuevas);
        Assert.Equal([fraude], _notificador.Intentos);
    }

    [Fact] // RF-28 CA3, HU-21 CA3
    public async Task En_alarma_otro_colado_se_suma_a_la_misma_alerta()
    {
        await Cruzar(trackId: 1);
        _reloj.Ahora = T0.AddSeconds(1);

        var registro = await Cruzar(trackId: 2);

        var alerta = Assert.Single(_alertas.Guardadas);
        Assert.Equal(alerta.Id, registro!.Cruce!.AlertaId);
        Assert.Equal(2, _intentos.Guardados.Count(i => i.AlertaId == alerta.Id));
        Assert.Single(_notificador.AlertasNuevas);
        Assert.Equal(2, _notificador.Intentos.Count);
    }

    [Fact] // RF-26 CA3
    public async Task Un_cruce_repetido_responde_duplicado_y_no_cambia_nada()
    {
        await Cruzar(trackId: 1);

        var registro = await Cruzar(trackId: 1);

        Assert.True(registro!.Duplicado);
        Assert.Single(_cruces.Guardados);
        Assert.Single(_intentos.Guardados);
        Assert.Single(_alertas.Guardadas);
    }

    [Fact] // RF-26 CA2, S7
    public async Task Un_cruce_de_salida_se_registra_sin_intento_ni_alerta()
    {
        var registro = await Cruzar(trackId: 1, SentidoCruce.Salida);

        Assert.Equal(TipoResultadoCruce.Registrado, registro!.Cruce!.Resultado);
        Assert.Empty(_intentos.Guardados);
        Assert.Empty(_alertas.Guardadas);
        Assert.Equal(EstadoTorniquete.Cerrado, _torniquete.Estado);
    }

    [Fact]
    public async Task Un_torniquete_que_no_existe_devuelve_null()
    {
        Assert.Null(await _registrarCruce.EjecutarAsync("tq_9", "v-1", 1, SentidoCruce.Entrada, T0));
    }
}
