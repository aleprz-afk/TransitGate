namespace TransitGate.Domain.Tarjetas;

/// <summary>
/// Una recarga con pago simulado (RF-15). Se guarda también la rechazada, sin tocar el saldo (CA2), y el
/// saldo resultante para el historial (RF-17 CA1).
/// </summary>
public class Recarga
{
    public Recarga(long id, Guid tarjetaId, decimal monto, bool aprobada, decimal saldoResultante, DateTime fecha, string? claveIdempotencia)
    {
        Id = id;
        TarjetaId = tarjetaId;
        Monto = monto;
        Aprobada = aprobada;
        SaldoResultante = saldoResultante;
        Fecha = fecha;
        ClaveIdempotencia = claveIdempotencia;
    }

    public long Id { get; private set; }

    public Guid TarjetaId { get; private set; }

    public decimal Monto { get; private set; }

    public bool Aprobada { get; private set; }

    public decimal SaldoResultante { get; private set; }

    public DateTime Fecha { get; private set; }

    /// <summary>La que manda el portal en cada intento de recarga: el doble clic no recarga dos veces (RF-15 CA5).</summary>
    public string? ClaveIdempotencia { get; private set; }
}
