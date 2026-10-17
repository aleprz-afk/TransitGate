namespace TransitGate.Domain.Tarjetas;

/// <summary>Por qué se bloqueó un soporte (RF-13, RF-14).</summary>
public enum MotivoBloqueo
{
    Perdida,
    Reporte,

    /// <summary>Se activó otro soporte del mismo cliente (S1).</summary>
    Reemplazo,
}
