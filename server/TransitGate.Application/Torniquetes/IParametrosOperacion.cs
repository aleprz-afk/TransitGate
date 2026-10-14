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
}
