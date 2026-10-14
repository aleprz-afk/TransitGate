using TransitGate.Domain.Accesos;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Domain.Tests.Accesos;

public class IntentoAccesoTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Un_rechazo_no_puede_llevar_el_motivo_de_fraude()
    {
        Assert.Throws<ArgumentException>(() => IntentoAcceso.Rechazado(
            1, "tq_1", "a1f3-0001", T0, TipoSoporte.Fisico, "04A1B2C3", null, MotivoIntento.FraudeSinValidacion));
    }

    [Fact] // RF-21 CA3: el intento guarda la tarifa que se cobró, aunque cambie después
    public void Un_intento_exitoso_guarda_tarifa_paso_y_saldo()
    {
        var tarjetaId = Guid.NewGuid();

        var intento = IntentoAcceso.Exitoso(
            1, "tq_1", "a1f3-0001", T0, TipoSoporte.Fisico, "04A1B2C3", tarjetaId, 3500, pasoId: 7, saldoRestante: 6500);

        Assert.Equal(ResultadoIntento.Exitoso, intento.Resultado);
        Assert.Equal(3500, intento.TarifaCobrada);
        Assert.Equal(7, intento.PasoId);
        Assert.Equal(6500, intento.SaldoRestante);
        Assert.Equal(tarjetaId, intento.TarjetaId);
    }
}
