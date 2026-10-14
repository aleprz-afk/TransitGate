namespace TransitGate.Domain.Torniquetes;

/// <summary>Lo que el ESP32 debe aplicar en el torniquete (RF-45).</summary>
public readonly record struct EstadoDeseado(bool BrazoAbierto, bool SirenaEncendida);
