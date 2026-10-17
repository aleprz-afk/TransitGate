namespace TransitGate.Domain.Tarjetas;

/// <summary>
/// La cuenta lógica: tiene el saldo y el historial (arquitectura v2 §4). Un cliente tiene exactamente una
/// (RF-07). Los soportes solo la identifican, así que bloquear o reemplazar uno nunca toca el saldo.
/// </summary>
public class Tarjeta
{
    public Tarjeta(Guid id, Guid clienteId)
    {
        Id = id;
        ClienteId = clienteId;
    }

    public Guid Id { get; private set; }

    public Guid ClienteId { get; private set; }

    public decimal Saldo { get; private set; }

    /// <summary>Descuenta el monto si alcanza; si no, no cambia nada (S3: no hay saldo negativo).</summary>
    public bool Debitar(decimal monto)
    {
        if (monto <= 0)
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto a debitar debe ser positivo.");
        if (Saldo < monto)
            return false;

        Saldo -= monto;
        return true;
    }

    /// <summary>Recarga aprobada (RF-15) o reversión de un débito (RF-46).</summary>
    public void Acreditar(decimal monto)
    {
        if (monto <= 0)
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto a acreditar debe ser positivo.");

        Saldo += monto;
    }
}
