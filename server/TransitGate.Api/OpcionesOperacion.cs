using Microsoft.Extensions.Options;
using TransitGate.Application.Torniquetes;

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

    /// <summary>S14: plazo sin latidos del ESP32 para marcarlo sin conexión.</summary>
    public int PlazoSenalEsp32Segundos { get; set; } = 5;

    /// <summary>S14: plazo sin señal de la visión para marcarla fuera de línea.</summary>
    public int PlazoSenalVisionSegundos { get; set; } = 15;
}

/// <summary>Lee la configuración en cada consulta, así un cambio en appsettings.json aplica sin reiniciar.</summary>
public sealed class ParametrosDesdeConfiguracion(IOptionsMonitor<OpcionesOperacion> opciones) : IParametrosOperacion
{
    public TimeSpan VentanaPaso => TimeSpan.FromSeconds(opciones.CurrentValue.ExpiracionPasoSegundos);

    public TimeSpan GraciaVision => TimeSpan.FromMilliseconds(opciones.CurrentValue.GraciaVisionMs);

    public TimeSpan PlazoSenalEsp32 => TimeSpan.FromSeconds(opciones.CurrentValue.PlazoSenalEsp32Segundos);

    public TimeSpan PlazoSenalVision => TimeSpan.FromSeconds(opciones.CurrentValue.PlazoSenalVisionSegundos);
}
