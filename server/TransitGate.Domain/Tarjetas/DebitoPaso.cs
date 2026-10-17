namespace TransitGate.Domain.Tarjetas;

/// <summary>
/// El cobro de un paso, guardado por pasoId para poder revertirlo si el brazo no abrió (RF-46, S15).
/// Se revierte una sola vez.
/// </summary>
public class DebitoPaso
{
    public DebitoPaso(long pasoId, Guid tarjetaId, decimal monto, DateTime fecha)
    {
        PasoId = pasoId;
        TarjetaId = tarjetaId;
        Monto = monto;
        Fecha = fecha;
    }

    public long PasoId { get; private set; }

    public Guid TarjetaId { get; private set; }

    public decimal Monto { get; private set; }

    public DateTime Fecha { get; private set; }

    public DateTime? RevertidoEn { get; private set; }

    /// <summary>Devuelve false si ya estaba revertido.</summary>
    public bool Revertir(DateTime fecha)
    {
        if (RevertidoEn is not null)
            return false;

        RevertidoEn = fecha;
        return true;
    }
}
