namespace TransitGate.Domain.Tarjetas;

/// <summary>
/// Con qué se usa la tarjeta (arquitectura v2 §4). El físico recorre Solicitada → Entregada → Activa y
/// puede terminar Bloqueada; una solicitud de tarjeta física es este mismo objeto en Solicitada (RF-08 CA1).
/// Las horas son UTC y las pasa quien llama.
/// </summary>
public class SoporteTarjeta
{
    private SoporteTarjeta(Guid id, Guid tarjetaId, TipoSoporte tipo, EstadoSoporte estado, DateTime fechaSolicitud)
    {
        Id = id;
        TarjetaId = tarjetaId;
        Tipo = tipo;
        Estado = estado;
        FechaSolicitud = fechaSolicitud;
    }

    public Guid Id { get; private set; }

    public Guid TarjetaId { get; private set; }

    public TipoSoporte Tipo { get; private set; }

    public EstadoSoporte Estado { get; private set; }

    /// <summary>UID de la MIFARE, en hexadecimal mayúsculas (RNF-35). Null hasta que se entrega en taquilla.</summary>
    public string? Uid { get; private set; }

    public MotivoBloqueo? MotivoBloqueo { get; private set; }

    public DateTime FechaSolicitud { get; private set; }

    public DateTime? FechaEntrega { get; private set; }

    public DateTime? FechaActivacion { get; private set; }

    public DateTime? FechaBloqueo { get; private set; }

    /// <summary>Un soporte que cuenta para "ya tienes una tarjeta física" (RF-08 CA2): todo menos Bloqueada.</summary>
    public bool Vigente => Estado != EstadoSoporte.Bloqueada;

    /// <summary>RF-08: el cliente pide la tarjeta física; queda Solicitada y sin UID.</summary>
    public static SoporteTarjeta SolicitarFisico(Guid id, Guid tarjetaId, DateTime fecha) =>
        new(id, tarjetaId, TipoSoporte.Fisico, EstadoSoporte.Solicitada, fecha);

    /// <summary>RF-10 CA1: en taquilla se liga el UID leído y la tarjeta queda Entregada.</summary>
    public void Entregar(string uid, DateTime fecha)
    {
        if (Estado != EstadoSoporte.Solicitada)
            throw new InvalidOperationException($"El soporte {Id} no está en Solicitada.");

        Uid = uid;
        Estado = EstadoSoporte.Entregada;
        FechaEntrega = fecha;
    }

    /// <summary>RF-11 CA1 (S5): el cliente confirma que la recibió y queda lista para pasar.</summary>
    public void Activar(DateTime fecha)
    {
        if (Estado != EstadoSoporte.Entregada)
            throw new InvalidOperationException($"El soporte {Id} no está en Entregada.");

        Estado = EstadoSoporte.Activa;
        FechaActivacion = fecha;
    }

    /// <summary>RF-13, RF-14. El saldo no cambia: es de la tarjeta.</summary>
    public void Bloquear(MotivoBloqueo motivo, DateTime fecha)
    {
        if (Estado == EstadoSoporte.Bloqueada)
            return;

        Estado = EstadoSoporte.Bloqueada;
        MotivoBloqueo = motivo;
        FechaBloqueo = fecha;
    }
}
