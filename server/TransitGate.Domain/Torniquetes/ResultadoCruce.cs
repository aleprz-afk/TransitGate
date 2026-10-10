namespace TransitGate.Domain.Torniquetes;

public enum TipoResultadoCruce
{
    Consumido,
    Fraude,
    Registrado,
}

/// <summary>
/// Lo que produjo un cruce: consumió un paso, fue fraude (con o sin alerta nueva)
/// o solo se registró, como los de salida.
/// </summary>
public sealed record ResultadoCruce(TipoResultadoCruce Tipo, PasoAutorizado? Paso, bool AlertaNueva)
{
    internal static ResultadoCruce Consumido(PasoAutorizado paso) => new(TipoResultadoCruce.Consumido, paso, false);

    internal static ResultadoCruce Fraude(bool alertaNueva) => new(TipoResultadoCruce.Fraude, null, alertaNueva);

    internal static ResultadoCruce Registrado() => new(TipoResultadoCruce.Registrado, null, false);
}
