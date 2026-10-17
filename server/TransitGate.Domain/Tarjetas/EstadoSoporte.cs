namespace TransitGate.Domain.Tarjetas;

/// <summary>
/// Estado de un soporte de tarjeta (RF v2, "Estados usados en los criterios"). El físico recorre los cuatro;
/// el virtual solo usa Activa y Bloqueada.
/// </summary>
public enum EstadoSoporte
{
    Solicitada,
    Entregada,
    Activa,
    Bloqueada,
}
