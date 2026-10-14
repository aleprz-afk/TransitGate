using TransitGate.Domain.Accesos;

namespace TransitGate.Domain.Alertas;

public enum EstadoAlerta
{
    Abierta,
    Atendida,
    Resuelta,
}

/// <summary>
/// Alerta de un colado (RF-28). Nace Abierta; al apagar la alarma queda Atendida (RF-34) y, al clasificarla,
/// Resuelta (RF-35, después del MVP). Los cruces que siguen mientras está abierta se ligan a ella (RF-28 CA3).
/// </summary>
public class AlertaSeguridad
{
    public AlertaSeguridad(long id, string torniqueteId, DateTime fecha)
    {
        Id = id;
        TorniqueteId = torniqueteId;
        Fecha = fecha;
    }

    public long Id { get; private set; }

    public string TorniqueteId { get; private set; }

    public MotivoIntento Tipo { get; private set; } = MotivoIntento.FraudeSinValidacion;

    public DateTime Fecha { get; private set; }

    public EstadoAlerta Estado { get; private set; } = EstadoAlerta.Abierta;

    public DateTime? AtendidaEn { get; private set; }

    /// <summary>El personal apagó la alarma (RF-34). Quién la atendió se agrega con Identity.</summary>
    public void Atender(DateTime ahora)
    {
        if (Estado != EstadoAlerta.Abierta)
            throw new InvalidOperationException($"La alerta {Id} ya está {Estado}.");

        Estado = EstadoAlerta.Atendida;
        AtendidaEn = ahora;
    }
}
