using TransitGate.Domain.Accesos;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Domain.Tests.Torniquetes;

public class TorniqueteTests
{
    // 08:00:00 en Colombia = 13:00:00 UTC (RNF-42).
    private static readonly DateTime T0 = new(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Ventana = TimeSpan.FromSeconds(10); // S4
    private static readonly TimeSpan Gracia = TimeSpan.FromSeconds(1);   // S13, valor mientras se mide

    // Estados que reporta el ESP32 en el latido.
    private static readonly EstadoDeseado Cerrado = new(BrazoAbierto: false, SirenaEncendida: false);
    private static readonly EstadoDeseado Abierto = new(BrazoAbierto: true, SirenaEncendida: false);

    private static DateTime Seg(double segundos) => T0.AddSeconds(segundos);

    private static Torniquete NuevoTorniquete() => new("tq_1", "Torniquete 1");

    private static Torniquete ConPasoAutorizado(out PasoAutorizado paso)
    {
        var torniquete = NuevoTorniquete();
        paso = torniquete.AutorizarPaso(123, T0, Ventana, Gracia);
        return torniquete;
    }

    private static Torniquete EnAlarma()
    {
        var torniquete = NuevoTorniquete();
        torniquete.RegistrarCruce(SentidoCruce.Entrada, T0, T0);
        return torniquete;
    }

    [Fact]
    public void Arranca_cerrado_con_brazo_cerrado_y_sirena_apagada()
    {
        var torniquete = NuevoTorniquete();

        Assert.Equal(EstadoTorniquete.Cerrado, torniquete.Estado);
        Assert.False(torniquete.BrazoDeseadoAbierto);
        Assert.False(torniquete.SirenaDeseada);
        Assert.Null(torniquete.MotivoNoDisponible);
    }

    [Fact] // RF-22 CA1 y CA3
    public void Autorizar_un_paso_abre_el_brazo_con_un_unico_paso_pendiente()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        Assert.Equal(EstadoTorniquete.EsperandoCruce, torniquete.Estado);
        Assert.True(torniquete.BrazoDeseadoAbierto);
        Assert.Same(paso, torniquete.PasoPendiente);
        Assert.Equal(EstadoPaso.Pendiente, paso.Estado);
        Assert.Equal(123, paso.Id);
    }

    [Fact] // RF-20 CA4
    public void Con_un_paso_pendiente_otra_lectura_se_rechaza_por_torniquete_ocupado()
    {
        var torniquete = ConPasoAutorizado(out _);

        Assert.Equal(MotivoIntento.TorniqueteOcupado, torniquete.MotivoNoDisponible);
        Assert.Throws<InvalidOperationException>(() => torniquete.AutorizarPaso(124, Seg(1), Ventana, Gracia));
    }

    [Fact] // RF-27 CA1 y CA2
    public void Un_cruce_de_entrada_dentro_de_la_ventana_consume_el_paso_y_cierra()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Entrada, horaFotograma: Seg(3), ahora: Seg(3.4));

        Assert.Equal(TipoResultadoCruce.Consumido, resultado.Tipo);
        Assert.Same(paso, resultado.Paso);
        Assert.Equal(EstadoPaso.Consumido, paso.Estado);
        Assert.Equal(Seg(3), paso.HoraCruce);
        Assert.Equal(EstadoTorniquete.Cerrado, torniquete.Estado);
        Assert.False(torniquete.BrazoDeseadoAbierto);
        Assert.Null(torniquete.PasoPendiente);
    }

    [Fact] // RF-29 CA2
    public void Un_cruce_que_llega_tarde_pero_dentro_de_la_gracia_consume_el_paso()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Entrada, horaFotograma: Seg(9.5), ahora: Seg(10.6));

        Assert.Equal(TipoResultadoCruce.Consumido, resultado.Tipo);
        Assert.Equal(EstadoPaso.Consumido, paso.Estado);
    }

    [Fact] // RF-29 CA1
    public void Sin_cruce_el_paso_expira_a_los_10_s_mas_la_gracia()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        torniquete.ActualizarPorTiempo(Seg(10.9));
        Assert.Equal(EstadoTorniquete.EsperandoCruce, torniquete.Estado);

        torniquete.ActualizarPorTiempo(Seg(11));
        Assert.Equal(EstadoPaso.Expirado, paso.Estado);
        Assert.Equal(EstadoTorniquete.Cerrado, torniquete.Estado);
        Assert.False(torniquete.BrazoDeseadoAbierto);
    }

    [Fact]
    public void Tras_expirar_el_paso_el_torniquete_acepta_otra_validacion()
    {
        var torniquete = ConPasoAutorizado(out var primero);

        var segundo = torniquete.AutorizarPaso(124, Seg(12), Ventana, Gracia);

        Assert.Equal(EstadoPaso.Expirado, primero.Estado);
        Assert.Same(segundo, torniquete.PasoPendiente);
        Assert.Equal(EstadoTorniquete.EsperandoCruce, torniquete.Estado);
    }

    [Fact] // RF-29 CA4
    public void Un_cruce_con_hora_posterior_a_la_ventana_es_fraude()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Entrada, horaFotograma: Seg(10.3), ahora: Seg(10.8));

        Assert.Equal(TipoResultadoCruce.Fraude, resultado.Tipo);
        Assert.True(resultado.AlertaNueva);
        Assert.Equal(EstadoPaso.Expirado, paso.Estado);
        Assert.Equal(EstadoTorniquete.Alarma, torniquete.Estado);
    }

    [Fact] // RF-28 CA1 y RF-45 CA3
    public void Un_cruce_de_entrada_sin_validacion_activa_la_alarma_con_el_brazo_cerrado()
    {
        var torniquete = NuevoTorniquete();

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Entrada, T0, T0);

        Assert.Equal(TipoResultadoCruce.Fraude, resultado.Tipo);
        Assert.True(resultado.AlertaNueva);
        Assert.Equal(EstadoTorniquete.Alarma, torniquete.Estado);
        Assert.True(torniquete.SirenaDeseada);
        Assert.False(torniquete.BrazoDeseadoAbierto);
    }

    [Fact] // RF-28 CA2
    public void Con_una_validacion_y_dos_cruces_el_segundo_es_fraude()
    {
        var torniquete = ConPasoAutorizado(out _);

        var primero = torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(2), Seg(2.5));
        var segundo = torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(2.6), Seg(3));

        Assert.Equal(TipoResultadoCruce.Consumido, primero.Tipo);
        Assert.Equal(TipoResultadoCruce.Fraude, segundo.Tipo);
        Assert.Equal(EstadoTorniquete.Alarma, torniquete.Estado);
    }

    [Fact] // RF-28 CA3
    public void En_alarma_otro_cruce_no_crea_una_alerta_nueva()
    {
        var torniquete = EnAlarma();

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(1), Seg(1));

        Assert.Equal(TipoResultadoCruce.Fraude, resultado.Tipo);
        Assert.False(resultado.AlertaNueva);
    }

    [Fact] // RF-20 CA5 y RF-28 CA4
    public void En_alarma_una_lectura_se_rechaza_por_torniquete_en_alarma()
    {
        var torniquete = EnAlarma();

        Assert.Equal(MotivoIntento.TorniqueteEnAlarma, torniquete.MotivoNoDisponible);
        Assert.Throws<InvalidOperationException>(() => torniquete.AutorizarPaso(124, Seg(1), Ventana, Gracia));
    }

    [Fact] // RF-34
    public void Apagar_la_alarma_vuelve_a_cerrado_y_permite_validar()
    {
        var torniquete = EnAlarma();

        torniquete.ApagarAlarma(Seg(30));

        Assert.Equal(EstadoTorniquete.Cerrado, torniquete.Estado);
        Assert.False(torniquete.SirenaDeseada);
        Assert.Null(torniquete.MotivoNoDisponible);
    }

    [Fact]
    public void Apagar_la_alarma_sin_alarma_es_un_error()
    {
        Assert.Throws<InvalidOperationException>(() => NuevoTorniquete().ApagarAlarma(T0));
    }

    [Fact] // RF-26 CA2 y S7
    public void Un_cruce_de_salida_no_consume_el_paso_ni_es_fraude()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Salida, Seg(2), Seg(2));

        Assert.Equal(TipoResultadoCruce.Registrado, resultado.Tipo);
        Assert.Equal(EstadoPaso.Pendiente, paso.Estado);
        Assert.Equal(EstadoTorniquete.EsperandoCruce, torniquete.Estado);
    }

    [Fact] // S6
    public void El_modo_registro_dura_como_maximo_60_s()
    {
        var torniquete = NuevoTorniquete();

        torniquete.ActivarModoRegistro(T0, TimeSpan.FromSeconds(60));
        Assert.Equal(EstadoTorniquete.ModoRegistro, torniquete.Estado);
        Assert.Equal(MotivoIntento.TorniqueteOcupado, torniquete.MotivoNoDisponible);

        torniquete.ActualizarPorTiempo(Seg(59));
        Assert.Equal(EstadoTorniquete.ModoRegistro, torniquete.Estado);

        torniquete.ActualizarPorTiempo(Seg(60));
        Assert.Equal(EstadoTorniquete.Cerrado, torniquete.Estado);
        Assert.Null(torniquete.ModoRegistroHasta);
    }

    [Fact] // RF-10
    public void Ligar_la_tarjeta_termina_el_modo_registro()
    {
        var torniquete = NuevoTorniquete();
        torniquete.ActivarModoRegistro(T0, TimeSpan.FromSeconds(60));

        torniquete.TerminarModoRegistro(Seg(20));

        Assert.Equal(EstadoTorniquete.Cerrado, torniquete.Estado);
    }

    [Fact] // RF-09: el contrato responde 409 TorniqueteOcupado
    public void No_se_puede_activar_el_modo_registro_con_un_paso_pendiente()
    {
        var torniquete = ConPasoAutorizado(out _);

        Assert.Throws<InvalidOperationException>(() => torniquete.ActivarModoRegistro(Seg(1), TimeSpan.FromSeconds(60)));
    }

    [Fact] // RF-28 CA5
    public void Un_cruce_en_modo_registro_es_fraude_y_cancela_el_modo()
    {
        var torniquete = NuevoTorniquete();
        torniquete.ActivarModoRegistro(T0, TimeSpan.FromSeconds(60));

        var resultado = torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(5), Seg(5));

        Assert.Equal(TipoResultadoCruce.Fraude, resultado.Tipo);
        Assert.True(resultado.AlertaNueva);
        Assert.Equal(EstadoTorniquete.Alarma, torniquete.Estado);
        Assert.Null(torniquete.ModoRegistroHasta);
    }

    [Fact] // RF-22 CA4
    public void El_latido_con_el_brazo_abierto_para_el_paso_confirma_la_apertura()
    {
        var torniquete = ConPasoAutorizado(out var paso);

        torniquete.RegistrarLatidoEsp32(Seg(0.3), Abierto, pasoIdAbierto: 999, arranque: "a1f3");
        Assert.Null(paso.AperturaConfirmadaEn);

        torniquete.RegistrarLatidoEsp32(Seg(0.6), Abierto, pasoIdAbierto: 123, arranque: "a1f3");
        Assert.Equal(Seg(0.6), paso.AperturaConfirmadaEn);
    }

    [Fact] // S14
    public void El_esp32_queda_sin_conexion_tras_5_s_sin_latidos()
    {
        var torniquete = NuevoTorniquete();
        var plazo = TimeSpan.FromSeconds(5);
        Assert.False(torniquete.Esp32Conectado(T0, plazo));

        torniquete.RegistrarLatidoEsp32(T0, Cerrado, pasoIdAbierto: null, arranque: "a1f3");

        Assert.True(torniquete.Esp32Conectado(Seg(4.9), plazo));
        Assert.False(torniquete.Esp32Conectado(Seg(5), plazo));
        Assert.Equal("a1f3", torniquete.ArranqueEsp32);
    }

    [Fact] // S14
    public void La_vision_queda_sin_conexion_tras_15_s_sin_senal()
    {
        var torniquete = NuevoTorniquete();
        var plazo = TimeSpan.FromSeconds(15);

        torniquete.RegistrarLatidoVision(T0);

        Assert.True(torniquete.VisionConectada(Seg(14), plazo));
        Assert.False(torniquete.VisionConectada(Seg(15), plazo));
    }

    [Fact] // RNF-04
    public void Cada_cambio_del_estado_deseado_guarda_desde_cuando_rige()
    {
        var torniquete = ConPasoAutorizado(out _);
        Assert.Equal(new EstadoDeseado(BrazoAbierto: true, SirenaEncendida: false), torniquete.Deseado);
        Assert.Equal(T0, torniquete.DeseadoDesde);

        // El vencimiento se fecha cuando vencía el plazo (10 s + 1 s), aunque el servidor lo note después.
        torniquete.ActualizarPorTiempo(Seg(11.2));
        Assert.Equal(new EstadoDeseado(false, false), torniquete.Deseado);
        Assert.Equal(Seg(11), torniquete.DeseadoDesde);

        torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(20), Seg(20));
        Assert.Equal(new EstadoDeseado(false, true), torniquete.Deseado);
        Assert.Equal(Seg(20), torniquete.DeseadoDesde);
    }

    [Fact] // RF-45 CA4
    public void Si_el_esp32_no_aplica_el_estado_deseado_por_mas_de_2_s_queda_desincronizado()
    {
        var torniquete = ConPasoAutorizado(out _);   // deseado: brazo abierto
        var tolerancia = TimeSpan.FromSeconds(2);

        torniquete.RegistrarLatidoEsp32(Seg(0.3), Cerrado, pasoIdAbierto: null, arranque: "a1f3");
        torniquete.RegistrarLatidoEsp32(Seg(2.3), Cerrado, pasoIdAbierto: null, arranque: "a1f3");

        Assert.False(torniquete.Desincronizado(Seg(2.3), tolerancia));
        Assert.True(torniquete.Desincronizado(Seg(2.4), tolerancia));
    }

    [Fact] // RF-45 CA4
    public void Un_latido_de_desfase_tras_un_cambio_no_desincroniza()
    {
        var torniquete = ConPasoAutorizado(out _);

        torniquete.RegistrarLatidoEsp32(Seg(0.3), Cerrado, pasoIdAbierto: null, arranque: "a1f3");
        torniquete.RegistrarLatidoEsp32(Seg(0.6), Abierto, pasoIdAbierto: 123, arranque: "a1f3");

        Assert.Null(torniquete.DistintoDelDeseadoDesde);
        Assert.False(torniquete.Desincronizado(Seg(5), TimeSpan.FromSeconds(2)));
    }

    [Fact] // RF-45 CA4
    public void Un_estado_deseado_nuevo_borra_la_desincronizacion_del_anterior()
    {
        var torniquete = EnAlarma();   // deseado: sirena encendida
        torniquete.RegistrarLatidoEsp32(Seg(0.3), Cerrado, pasoIdAbierto: null, arranque: "a1f3");
        torniquete.RegistrarLatidoEsp32(Seg(3), Cerrado, pasoIdAbierto: null, arranque: "a1f3");
        Assert.True(torniquete.Desincronizado(Seg(3), TimeSpan.FromSeconds(2)));

        torniquete.ApagarAlarma(Seg(3.1));

        Assert.False(torniquete.Desincronizado(Seg(3.1), TimeSpan.FromSeconds(2)));
    }

    [Fact] // HU-20 CA2
    public void Cada_cambio_de_estado_guarda_desde_cuando_esta_asi()
    {
        var torniquete = NuevoTorniquete();
        Assert.Null(torniquete.EstadoDesde);

        torniquete.ActivarModoRegistro(T0, TimeSpan.FromSeconds(60));
        Assert.Equal(T0, torniquete.EstadoDesde);

        torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(5), Seg(5));      // fraude: Alarma
        Assert.Equal(Seg(5), torniquete.EstadoDesde);

        torniquete.RegistrarCruce(SentidoCruce.Entrada, Seg(8), Seg(8));      // sigue en Alarma
        Assert.Equal(Seg(5), torniquete.EstadoDesde);
    }

    [Fact]
    public void Entrar_en_modo_registro_no_cambia_el_estado_deseado()
    {
        var torniquete = NuevoTorniquete();

        torniquete.ActivarModoRegistro(T0, TimeSpan.FromSeconds(60));

        Assert.Null(torniquete.DeseadoDesde);
    }
}
