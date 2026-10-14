namespace TransitGate.Domain.Torniquetes;

/// <summary>
/// Un cruce de la línea virtual reportado por la visión (RF-26 CA1). Solo el evento: sin imágenes ni
/// coordenadas de la persona (RF-26 CA4, RNF-26).
/// </summary>
public class Cruce
{
    public Cruce(
        long id,
        string torniqueteId,
        string sesionVision,
        long trackId,
        SentidoCruce sentido,
        DateTime horaFotograma,
        DateTime llegada,
        ResultadoCruce resultado,
        long? alertaId)
    {
        Id = id;
        TorniqueteId = torniqueteId;
        SesionVision = sesionVision;
        TrackId = trackId;
        Sentido = sentido;
        HoraFotograma = horaFotograma;
        Llegada = llegada;
        Resultado = resultado.Tipo;
        PasoId = resultado.Paso?.Id;
        AlertaId = alertaId;
    }

    public long Id { get; private set; }

    public string TorniqueteId { get; private set; }

    /// <summary>Junto con TrackId, la clave de idempotencia (RF-26 CA3).</summary>
    public string SesionVision { get; private set; }

    public long TrackId { get; private set; }

    public SentidoCruce Sentido { get; private set; }

    /// <summary>Hora en que el fotograma llegó al servicio de visión; con ella se juzga el cruce (S13).</summary>
    public DateTime HoraFotograma { get; private set; }

    /// <summary>Hora en que el reporte llegó a la API (RNF-06).</summary>
    public DateTime Llegada { get; private set; }

    public TipoResultadoCruce Resultado { get; private set; }

    /// <summary>Paso que consumió (RF-27 CA2).</summary>
    public long? PasoId { get; private set; }

    /// <summary>Alerta a la que se ligó si fue fraude (RF-28 CA3).</summary>
    public long? AlertaId { get; private set; }
}
