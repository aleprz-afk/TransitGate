namespace TransitGate.Api;

/// <summary>
/// Tiempos de operación con sus valores por defecto (RNF-40): se cambian en appsettings.json sin recompilar.
/// Cuando exista RF-44, lo que el administrador guarde en la base de datos prevalece.
/// </summary>
public sealed class OpcionesOperacion
{
    public const string Seccion = "Operacion";

    /// <summary>S4: duración de la ventana de un paso autorizado.</summary>
    public int ExpiracionPasoSegundos { get; set; } = 10;

    /// <summary>S13: gracia de visión; vale 1 s hasta que se mida con RNF-06.</summary>
    public int GraciaVisionMs { get; set; } = 1000;
}
