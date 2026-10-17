using TransitGate.Domain.Tarjetas;

namespace TransitGate.Domain.Tests.Tarjetas;

public class TarjetaTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 13, 0, 0, DateTimeKind.Utc);

    [Fact] // RF-07 CA1
    public void Una_tarjeta_nueva_empieza_con_saldo_cero()
    {
        Assert.Equal(0, new Tarjeta(Guid.NewGuid(), Guid.NewGuid()).Saldo);
    }

    [Fact] // S3
    public void Si_el_saldo_no_alcanza_no_se_debita_nada()
    {
        var tarjeta = new Tarjeta(Guid.NewGuid(), Guid.NewGuid());
        tarjeta.Acreditar(2000);

        Assert.False(tarjeta.Debitar(3200));
        Assert.Equal(2000, tarjeta.Saldo);

        Assert.True(tarjeta.Debitar(2000));
        Assert.Equal(0, tarjeta.Saldo);
    }

    [Fact] // RF-08 CA1, RF-10 CA1, RF-11 CA1
    public void La_tarjeta_fisica_va_de_solicitada_a_entregada_y_a_activa()
    {
        var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), Guid.NewGuid(), T0);
        Assert.Equal(EstadoSoporte.Solicitada, soporte.Estado);
        Assert.Null(soporte.Uid);

        soporte.Entregar("04A1B2C3", T0.AddDays(1));
        Assert.Equal(EstadoSoporte.Entregada, soporte.Estado);
        Assert.Equal("04A1B2C3", soporte.Uid);

        soporte.Activar(T0.AddDays(2));
        Assert.Equal(EstadoSoporte.Activa, soporte.Estado);
        Assert.Equal(T0.AddDays(2), soporte.FechaActivacion);
    }

    [Fact] // RF-11 CA3
    public void Solo_se_activa_una_tarjeta_entregada()
    {
        var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), Guid.NewGuid(), T0);

        Assert.Throws<InvalidOperationException>(() => soporte.Activar(T0));
    }

    [Fact]
    public void Solo_se_entrega_una_tarjeta_solicitada()
    {
        var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), Guid.NewGuid(), T0);
        soporte.Entregar("04A1B2C3", T0);

        Assert.Throws<InvalidOperationException>(() => soporte.Entregar("DEADBEEF", T0));
    }

    [Fact] // RF-14
    public void Bloquear_guarda_el_motivo_y_deja_de_estar_vigente()
    {
        var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), Guid.NewGuid(), T0);

        soporte.Bloquear(MotivoBloqueo.Perdida, T0);
        soporte.Bloquear(MotivoBloqueo.Reporte, T0.AddHours(1));

        Assert.Equal(EstadoSoporte.Bloqueada, soporte.Estado);
        Assert.Equal(MotivoBloqueo.Perdida, soporte.MotivoBloqueo);
        Assert.False(soporte.Vigente);
    }

    [Fact] // RF-46
    public void Un_debito_se_revierte_una_sola_vez()
    {
        var debito = new DebitoPaso(7, Guid.NewGuid(), 3200, T0);

        Assert.True(debito.Revertir(T0.AddSeconds(2)));
        Assert.False(debito.Revertir(T0.AddSeconds(3)));
        Assert.Equal(T0.AddSeconds(2), debito.RevertidoEn);
    }
}
