using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;

namespace TransitGate.Domain.Tests.Alertas;

public class AlertaSeguridadTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 13, 0, 0, DateTimeKind.Utc);

    [Fact] // RF-28 CA1
    public void Una_alerta_nueva_esta_abierta_y_es_de_fraude_sin_validacion()
    {
        var alerta = new AlertaSeguridad(1, "tq_1", T0);

        Assert.Equal(EstadoAlerta.Abierta, alerta.Estado);
        Assert.Equal(MotivoIntento.FraudeSinValidacion, alerta.Tipo);
    }

    [Fact] // RF-34, HU-22 CA1
    public void Apagar_la_alarma_deja_la_alerta_atendida_con_la_hora()
    {
        var alerta = new AlertaSeguridad(1, "tq_1", T0);

        alerta.Atender(T0.AddSeconds(40));

        Assert.Equal(EstadoAlerta.Atendida, alerta.Estado);
        Assert.Equal(T0.AddSeconds(40), alerta.AtendidaEn);
    }

    [Fact]
    public void Una_alerta_ya_atendida_no_se_atiende_otra_vez()
    {
        var alerta = new AlertaSeguridad(1, "tq_1", T0);
        alerta.Atender(T0);

        Assert.Throws<InvalidOperationException>(() => alerta.Atender(T0));
    }
}
