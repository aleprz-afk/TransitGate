namespace TransitGate.Domain.Torniquetes;

/// <summary>
/// Estados del torniquete (arquitectura v2 §4.1). La conexión no es un estado:
/// se lleva aparte, como la hora de la última señal de vida.
/// </summary>
public enum EstadoTorniquete
{
    Cerrado,
    EsperandoCruce,
    Alarma,
    ModoRegistro,
}
