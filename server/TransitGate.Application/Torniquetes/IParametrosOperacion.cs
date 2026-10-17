namespace TransitGate.Application.Torniquetes;

/// <summary>
/// Tiempos de operación. Hoy salen de appsettings.json; con RF-44 prevalece lo que guarde
/// el administrador en la base de datos (RNF-40).
/// </summary>
public interface IParametrosOperacion
{
    /// <summary>S4: ventana de un paso autorizado.</summary>
    TimeSpan VentanaPaso { get; }

    /// <summary>S13: gracia de visión.</summary>
    TimeSpan GraciaVision { get; }

    /// <summary>S14: sin latidos del ESP32 durante este plazo, el torniquete figura sin conexión.</summary>
    TimeSpan PlazoSenalEsp32 { get; }

    /// <summary>S14: sin señal de la visión durante este plazo, figura fuera de línea (RF-30).</summary>
    TimeSpan PlazoSenalVision { get; }

    /// <summary>S6: duración máxima del modo registro.</summary>
    TimeSpan DuracionModoRegistro { get; }
}
