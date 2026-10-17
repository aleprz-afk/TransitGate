namespace TransitGate.Application.Cuentas;

/// <summary>
/// Tarifa y límites de recarga. Hoy salen de appsettings.json; con RF-41 y RF-44 prevalece lo que guarde
/// el administrador (RNF-40).
/// </summary>
public interface IParametrosCuentas
{
    /// <summary>Tarifa del paso; en el MVP, 3.200 fija (MVP §3.2, RF-41).</summary>
    decimal Tarifa { get; }

    /// <summary>S8: mínimo por recarga.</summary>
    decimal MontoMinRecarga { get; }

    /// <summary>S8: máximo por recarga.</summary>
    decimal MontoMaxRecarga { get; }
}
