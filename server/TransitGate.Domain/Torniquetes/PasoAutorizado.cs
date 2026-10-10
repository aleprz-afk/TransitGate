namespace TransitGate.Domain.Torniquetes;

/// <summary>
/// Paso concedido por una validación exitosa (RF-22). Nace Pendiente y termina
/// Consumido (RF-27), Expirado (RF-29) o Revertido (RF-46).
/// </summary>
public class PasoAutorizado
{
    internal PasoAutorizado(long id, DateTime autorizadoEn, DateTime finVentana, DateTime expiraEn)
    {
        Id = id;
        AutorizadoEn = autorizadoEn;
        FinVentana = finVentana;
        ExpiraEn = expiraEn;
    }

    public long Id { get; private set; }

    public DateTime AutorizadoEn { get; private set; }

    /// <summary>Hora de fotograma más tardía con la que un cruce todavía consume el paso (S4).</summary>
    public DateTime FinVentana { get; private set; }

    /// <summary>FinVentana más la gracia de visión (S13): desde aquí el paso se da por expirado.</summary>
    public DateTime ExpiraEn { get; private set; }

    public EstadoPaso Estado { get; private set; } = EstadoPaso.Pendiente;

    /// <summary>Hora del fotograma del cruce que consumió el paso (RF-27 CA2).</summary>
    public DateTime? HoraCruce { get; private set; }

    internal bool AdmiteCruce(DateTime horaFotograma) =>
        horaFotograma >= AutorizadoEn && horaFotograma <= FinVentana;

    internal void Consumir(DateTime horaFotograma)
    {
        AsegurarPendiente();
        Estado = EstadoPaso.Consumido;
        HoraCruce = horaFotograma;
    }

    internal void Expirar()
    {
        AsegurarPendiente();
        Estado = EstadoPaso.Expirado;
    }

    private void AsegurarPendiente()
    {
        if (Estado != EstadoPaso.Pendiente)
            throw new InvalidOperationException($"El paso {Id} ya está {Estado}.");
    }
}
