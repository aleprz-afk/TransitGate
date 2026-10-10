namespace TransitGate.Domain.Accesos;

/// <summary>Catálogo de motivos de un IntentoAcceso que no fue exitoso (RF v2, sección 1).</summary>
public enum MotivoIntento
{
    TarjetaNoRegistrada,
    SoporteNoActivo,
    SoporteBloqueado,
    SaldoInsuficiente,
    FirmaInvalida,
    DesafioInvalido,
    TorniqueteOcupado,
    TorniqueteEnAlarma,
    FraudeSinValidacion,
}
