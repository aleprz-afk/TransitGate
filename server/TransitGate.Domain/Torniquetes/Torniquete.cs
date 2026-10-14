using TransitGate.Domain.Accesos;

namespace TransitGate.Domain.Torniquetes;

/// <summary>
/// Máquina de estados del torniquete (arquitectura v2 §4.1). El servidor decide y el ESP32
/// solo aplica el estado deseado que recibe en cada latido (RF-45).
/// Las horas son UTC y las pasa quien llama, para poder probar sin un reloj real.
/// </summary>
public class Torniquete
{
    public Torniquete(string id, string nombre)
    {
        Id = id;
        Nombre = nombre;
    }

    public string Id { get; private set; }

    public string Nombre { get; private set; }

    public EstadoTorniquete Estado { get; private set; } = EstadoTorniquete.Cerrado;

    /// <summary>El único paso Pendiente mientras el torniquete está en EsperandoCruce (RF-22 CA3).</summary>
    public PasoAutorizado? PasoPendiente { get; private set; }

    public DateTime? ModoRegistroHasta { get; private set; }

    /// <summary>Último latido del ESP32. La conexión no es un estado: se deduce de esta hora (S14).</summary>
    public DateTime? UltimaSenalEsp32 { get; private set; }

    /// <summary>Identificador de arranque del ESP32; cambia en cada reinicio (RF-45 CA3).</summary>
    public string? ArranqueEsp32 { get; private set; }

    public DateTime? UltimaSenalVision { get; private set; }

    // Estado deseado que recibe el ESP32. En Alarma el brazo queda cerrado y la sirena suena (RF-45 CA3).
    public bool BrazoDeseadoAbierto => Estado == EstadoTorniquete.EsperandoCruce;

    public bool SirenaDeseada => Estado == EstadoTorniquete.Alarma;

    public EstadoDeseado Deseado => new(BrazoDeseadoAbierto, SirenaDeseada);

    /// <summary>
    /// Desde cuándo rige el estado deseado actual; con esta hora se mide RNF-04. En un vencimiento es la
    /// hora en que vencía el plazo, no la hora en que el servidor lo notó.
    /// </summary>
    public DateTime? DeseadoDesde { get; private set; }

    /// <summary>
    /// Motivo por el que ahora no se puede autorizar un paso (primer chequeo de RF-20), o null si está libre.
    /// Conviene llamar antes a <see cref="ActualizarPorTiempo"/> para no rechazar por un paso ya vencido.
    /// </summary>
    public MotivoIntento? MotivoNoDisponible => Estado switch
    {
        EstadoTorniquete.Cerrado => null,
        EstadoTorniquete.Alarma => MotivoIntento.TorniqueteEnAlarma,
        _ => MotivoIntento.TorniqueteOcupado,
    };

    public bool Esp32Conectado(DateTime ahora, TimeSpan plazo) =>
        UltimaSenalEsp32 is { } senal && ahora - senal < plazo;

    public bool VisionConectada(DateTime ahora, TimeSpan plazo) =>
        UltimaSenalVision is { } senal && ahora - senal < plazo;

    /// <summary>Aplica los vencimientos: el paso sin cruce (RF-29) y el fin del modo registro (S6).</summary>
    public void ActualizarPorTiempo(DateTime ahora)
    {
        if (Estado == EstadoTorniquete.EsperandoCruce && ahora >= PasoPendiente!.ExpiraEn)
        {
            var vencimiento = PasoPendiente.ExpiraEn;
            PasoPendiente.Expirar();
            PasoPendiente = null;
            CambiarEstado(EstadoTorniquete.Cerrado, vencimiento);
        }
        else if (Estado == EstadoTorniquete.ModoRegistro && ahora >= ModoRegistroHasta)
        {
            SalirDeModoRegistro(ModoRegistroHasta.Value);
        }
    }

    /// <summary>
    /// Latido del ESP32 (RF-45, S11): registra la señal de vida y, si reporta el brazo abierto para el paso
    /// pendiente, confirma la apertura (RF-22 CA4).
    /// </summary>
    public void RegistrarLatidoEsp32(DateTime ahora, long? pasoIdAbierto, string arranque)
    {
        UltimaSenalEsp32 = ahora;
        ArranqueEsp32 = arranque;

        if (pasoIdAbierto is { } pasoId && PasoPendiente?.Id == pasoId)
            PasoPendiente.ConfirmarApertura(ahora);
    }

    public void RegistrarLatidoVision(DateTime ahora) => UltimaSenalVision = ahora;

    /// <summary>
    /// Abre la ventana de exactamente un paso (RF-22). Un cruce cuenta si su fotograma cae dentro de
    /// <paramref name="ventana"/> (S4); el paso se declara expirado tras esperar además la gracia de visión (S13).
    /// </summary>
    public PasoAutorizado AutorizarPaso(long pasoId, DateTime ahora, TimeSpan ventana, TimeSpan gracia)
    {
        ActualizarPorTiempo(ahora);
        if (Estado != EstadoTorniquete.Cerrado)
            throw new InvalidOperationException($"El torniquete {Id} no puede autorizar un paso en estado {Estado}.");

        PasoPendiente = new PasoAutorizado(pasoId, ahora, ahora + ventana, ahora + ventana + gracia);
        CambiarEstado(EstadoTorniquete.EsperandoCruce, ahora);
        return PasoPendiente;
    }

    /// <summary>
    /// Evalúa un cruce reportado por la visión (RF-26 a RF-29). Se juzga por la hora del fotograma (S13),
    /// no por la hora en que el reporte llega a la API.
    /// </summary>
    public ResultadoCruce RegistrarCruce(SentidoCruce sentido, DateTime horaFotograma, DateTime ahora)
    {
        ActualizarPorTiempo(ahora);

        if (sentido == SentidoCruce.Salida)
            return ResultadoCruce.Registrado();

        if (Estado == EstadoTorniquete.EsperandoCruce && PasoPendiente!.AdmiteCruce(horaFotograma))
        {
            var paso = PasoPendiente;
            paso.Consumir(horaFotograma);
            PasoPendiente = null;
            CambiarEstado(EstadoTorniquete.Cerrado, ahora);
            return ResultadoCruce.Consumido(paso);
        }

        // Cruce de entrada sin paso disponible: fraude (RF-28).
        if (Estado == EstadoTorniquete.Alarma)
            return ResultadoCruce.Fraude(alertaNueva: false);

        PasoPendiente?.Expirar();   // el cruce cayó fuera de la ventana (RF-29 CA4)
        PasoPendiente = null;
        ModoRegistroHasta = null;   // RF-28 CA5: se cancela el modo registro
        CambiarEstado(EstadoTorniquete.Alarma, ahora);
        return ResultadoCruce.Fraude(alertaNueva: true);
    }

    /// <summary>El personal apaga la alarma desde el panel (RF-34).</summary>
    public void ApagarAlarma(DateTime ahora)
    {
        if (Estado != EstadoTorniquete.Alarma)
            throw new InvalidOperationException($"El torniquete {Id} no está en alarma.");

        CambiarEstado(EstadoTorniquete.Cerrado, ahora);
    }

    /// <summary>El operador pone el lector en modo registro para ligar una tarjeta nueva (RF-09, S6).</summary>
    public void ActivarModoRegistro(DateTime ahora, TimeSpan duracion)
    {
        ActualizarPorTiempo(ahora);
        if (Estado != EstadoTorniquete.Cerrado)
            throw new InvalidOperationException($"El torniquete {Id} no puede entrar en modo registro en estado {Estado}.");

        CambiarEstado(EstadoTorniquete.ModoRegistro, ahora);
        ModoRegistroHasta = ahora + duracion;
    }

    /// <summary>El UID leído ya se ligó a la solicitud (RF-10): el torniquete vuelve a Cerrado.</summary>
    public void TerminarModoRegistro(DateTime ahora)
    {
        if (Estado != EstadoTorniquete.ModoRegistro)
            throw new InvalidOperationException($"El torniquete {Id} no está en modo registro.");

        SalirDeModoRegistro(ahora);
    }

    private void SalirDeModoRegistro(DateTime momento)
    {
        ModoRegistroHasta = null;
        CambiarEstado(EstadoTorniquete.Cerrado, momento);
    }

    private void CambiarEstado(EstadoTorniquete nuevo, DateTime momento)
    {
        var antes = Deseado;
        Estado = nuevo;
        if (Deseado != antes)
            DeseadoDesde = momento;
    }
}
