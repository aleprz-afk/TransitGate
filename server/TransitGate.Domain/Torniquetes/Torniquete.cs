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

    // Estado deseado que recibe el ESP32. En Alarma el brazo queda cerrado y la sirena suena (RF-45 CA3).
    public bool BrazoDeseadoAbierto => Estado == EstadoTorniquete.EsperandoCruce;

    public bool SirenaDeseada => Estado == EstadoTorniquete.Alarma;

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

    /// <summary>Aplica los vencimientos: el paso sin cruce (RF-29) y el fin del modo registro (S6).</summary>
    public void ActualizarPorTiempo(DateTime ahora)
    {
        if (Estado == EstadoTorniquete.EsperandoCruce && ahora >= PasoPendiente!.ExpiraEn)
        {
            PasoPendiente.Expirar();
            PasoPendiente = null;
            Estado = EstadoTorniquete.Cerrado;
        }
        else if (Estado == EstadoTorniquete.ModoRegistro && ahora >= ModoRegistroHasta)
        {
            SalirDeModoRegistro();
        }
    }

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
        Estado = EstadoTorniquete.EsperandoCruce;
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
            Estado = EstadoTorniquete.Cerrado;
            return ResultadoCruce.Consumido(paso);
        }

        // Cruce de entrada sin paso disponible: fraude (RF-28).
        if (Estado == EstadoTorniquete.Alarma)
            return ResultadoCruce.Fraude(alertaNueva: false);

        PasoPendiente?.Expirar();   // el cruce cayó fuera de la ventana (RF-29 CA4)
        PasoPendiente = null;
        ModoRegistroHasta = null;   // RF-28 CA5: se cancela el modo registro
        Estado = EstadoTorniquete.Alarma;
        return ResultadoCruce.Fraude(alertaNueva: true);
    }

    /// <summary>El personal apaga la alarma desde el panel (RF-34).</summary>
    public void ApagarAlarma()
    {
        if (Estado != EstadoTorniquete.Alarma)
            throw new InvalidOperationException($"El torniquete {Id} no está en alarma.");

        Estado = EstadoTorniquete.Cerrado;
    }

    /// <summary>El operador pone el lector en modo registro para ligar una tarjeta nueva (RF-09, S6).</summary>
    public void ActivarModoRegistro(DateTime ahora, TimeSpan duracion)
    {
        ActualizarPorTiempo(ahora);
        if (Estado != EstadoTorniquete.Cerrado)
            throw new InvalidOperationException($"El torniquete {Id} no puede entrar en modo registro en estado {Estado}.");

        Estado = EstadoTorniquete.ModoRegistro;
        ModoRegistroHasta = ahora + duracion;
    }

    /// <summary>El UID leído ya se ligó a la solicitud (RF-10): el torniquete vuelve a Cerrado.</summary>
    public void TerminarModoRegistro()
    {
        if (Estado != EstadoTorniquete.ModoRegistro)
            throw new InvalidOperationException($"El torniquete {Id} no está en modo registro.");

        SalirDeModoRegistro();
    }

    private void SalirDeModoRegistro()
    {
        ModoRegistroHasta = null;
        Estado = EstadoTorniquete.Cerrado;
    }
}
