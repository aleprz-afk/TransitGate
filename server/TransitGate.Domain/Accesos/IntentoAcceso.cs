using TransitGate.Domain.Tarjetas;

namespace TransitGate.Domain.Accesos;

/// <summary>
/// Registro de cada lectura procesada fuera de modo registro (RF-23) y de cada cruce sin validación (RF-28).
/// Es la base del historial del panel (RF-32) y del cliente (RF-39), y la respuesta guardada para los
/// reintentos con el mismo lecturaId (RF-18 CA4).
/// </summary>
public class IntentoAcceso
{
    private IntentoAcceso(long id, string torniqueteId, DateTime fecha, ResultadoIntento resultado)
    {
        Id = id;
        TorniqueteId = torniqueteId;
        Fecha = fecha;
        Resultado = resultado;
    }

    public long Id { get; private set; }

    public string TorniqueteId { get; private set; }

    /// <summary>Identificador que genera el ESP32 para cada lectura (RF-18). Null en un fraude: no hubo lectura.</summary>
    public string? LecturaId { get; private set; }

    public DateTime Fecha { get; private set; }

    /// <summary>Null en un fraude: nadie acercó una tarjeta.</summary>
    public TipoSoporte? TipoSoporte { get; private set; }

    /// <summary>UID leído, aun si no corresponde a ninguna tarjeta (RF-23 CA2). El panel solo muestra los 4 últimos.</summary>
    public string? UidLeido { get; private set; }

    /// <summary>Null si el UID no corresponde a ningún soporte, o en un fraude.</summary>
    public Guid? TarjetaId { get; private set; }

    public ResultadoIntento Resultado { get; private set; }

    public MotivoIntento? Motivo { get; private set; }

    /// <summary>Tarifa cobrada; queda guardada aunque la tarifa cambie después (RF-21 CA3).</summary>
    public decimal? TarifaCobrada { get; private set; }

    public long? PasoId { get; private set; }

    public decimal? SaldoRestante { get; private set; }

    /// <summary>Alerta a la que se ligó el fraude (RF-28).</summary>
    public long? AlertaId { get; private set; }

    public static IntentoAcceso Exitoso(
        long id,
        string torniqueteId,
        string lecturaId,
        DateTime fecha,
        TipoSoporte tipoSoporte,
        string? uidLeido,
        Guid tarjetaId,
        decimal tarifaCobrada,
        long pasoId,
        decimal saldoRestante) =>
        new(id, torniqueteId, fecha, ResultadoIntento.Exitoso)
        {
            LecturaId = lecturaId,
            TipoSoporte = tipoSoporte,
            UidLeido = uidLeido,
            TarjetaId = tarjetaId,
            TarifaCobrada = tarifaCobrada,
            PasoId = pasoId,
            SaldoRestante = saldoRestante,
        };

    public static IntentoAcceso Rechazado(
        long id,
        string torniqueteId,
        string lecturaId,
        DateTime fecha,
        TipoSoporte tipoSoporte,
        string? uidLeido,
        Guid? tarjetaId,
        MotivoIntento motivo)
    {
        if (motivo == MotivoIntento.FraudeSinValidacion)
            throw new ArgumentException("FraudeSinValidacion es el motivo de un intento de fraude, no de un rechazo.", nameof(motivo));

        return new(id, torniqueteId, fecha, ResultadoIntento.Rechazado)
        {
            LecturaId = lecturaId,
            TipoSoporte = tipoSoporte,
            UidLeido = uidLeido,
            TarjetaId = tarjetaId,
            Motivo = motivo,
        };
    }

    /// <summary>Un cruce de entrada sin paso disponible (RF-28 CA1).</summary>
    public static IntentoAcceso Fraude(long id, string torniqueteId, DateTime fecha, long alertaId) =>
        new(id, torniqueteId, fecha, ResultadoIntento.Fraude)
        {
            Motivo = MotivoIntento.FraudeSinValidacion,
            AlertaId = alertaId,
        };
}
