using TransitGate.Application.Torniquetes;

namespace TransitGate.Api;

/// <summary>
/// Tiempos de operación con sus valores por defecto (RNF-40): se cambian en appsettings.json sin recompilar.
/// Cuando exista RF-44, lo que el administrador guarde en la base de datos prevalece.
/// </summary>
public sealed class OpcionesOperacion : IParametrosOperacion
{
    public const string Seccion = "Operacion";

    /// <summary>S4: duración de la ventana de un paso autorizado.</summary>
    public int ExpiracionPasoSegundos { get; set; } = 10;

    /// <summary>S13: gracia de visión; vale 1 s hasta que se mida con RNF-06.</summary>
    public int GraciaVisionMs { get; set; } = 1000;

    /// <summary>S14: plazo sin latidos del ESP32 para marcarlo sin conexión.</summary>
    public int PlazoSenalEsp32Segundos { get; set; } = 5;

    public TimeSpan VentanaPaso => TimeSpan.FromSeconds(ExpiracionPasoSegundos);

    public TimeSpan GraciaVision => TimeSpan.FromMilliseconds(GraciaVisionMs);

    public TimeSpan PlazoSenalEsp32 => TimeSpan.FromSeconds(PlazoSenalEsp32Segundos);
}
