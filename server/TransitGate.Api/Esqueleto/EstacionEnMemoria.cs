using Microsoft.Extensions.Options;
using TransitGate.Api.Contratos;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Esqueleto;

/// <summary>
/// API esqueleto de la semana 1: el torniquete semilla vive en memoria y usa la máquina de estados
/// real del dominio, pero no hay base de datos, ni tarjetas reales, y el saldo es fijo.
/// Sirve para que el firmware (sim-esp32) y la visión (sim-vision) prueben el contrato desde ya.
/// En la semana 2 lo reemplazan los casos de uso de Application con ISoporteResolver e ISaldoService (C7).
/// </summary>
public sealed class EstacionEnMemoria(TimeProvider reloj, IOptionsMonitor<OpcionesOperacion> opciones)
{
    public const string TorniqueteSemilla = "tq_1";

    private const int SaldoFijo = 8800;

    /// <summary>UIDs para provocar rechazos desde sim-esp32; cualquier otro UID válido abre.</summary>
    public static readonly IReadOnlyDictionary<string, MotivoIntento> UidsDeRechazo = new Dictionary<string, MotivoIntento>
    {
        ["00000000"] = MotivoIntento.TarjetaNoRegistrada,
        ["DEADBEEF"] = MotivoIntento.SaldoInsuficiente,
    };

    // El celular es Fase 4: por ahora el latido trae siempre el desafío del vector de prueba de C2 (16 bytes en 0).
    private static readonly DesafioLatido DesafioFijo = new("d_esqueleto", new string('0', 32), 30);

    private readonly Lock _cerrojo = new();
    private readonly Dictionary<string, Torniquete> _torniquetes = new()
    {
        [TorniqueteSemilla] = new Torniquete(TorniqueteSemilla, "Torniquete 1"),
    };
    private readonly Dictionary<(string Torniquete, string LecturaId), RespuestaLectura> _lecturas = [];
    private readonly HashSet<(string Torniquete, string SesionVision, long TrackId)> _cruces = [];
    private readonly Dictionary<string, AlertaEnMemoria> _alertas = [];
    private long _ultimoPasoId;
    private int _ultimaAlertaId;

    public bool Existe(string torniqueteId) => _torniquetes.ContainsKey(torniqueteId);

    public RespuestaLectura RecibirLectura(string torniqueteId, string lecturaId, string uid)
    {
        lock (_cerrojo)
        {
            // RF-18 CA4: un reintento con el mismo lecturaId recibe la misma respuesta y no cobra otra vez.
            if (_lecturas.TryGetValue((torniqueteId, lecturaId), out var previa))
                return previa;

            var torniquete = _torniquetes[torniqueteId];
            var ahora = Ahora();
            torniquete.ActualizarPorTiempo(ahora);

            RespuestaLectura respuesta;
            if (torniquete.MotivoNoDisponible is { } motivoTorniquete)
            {
                respuesta = RespuestaLectura.Denegar(motivoTorniquete);
            }
            else if (UidsDeRechazo.TryGetValue(uid, out var motivoTarjeta))
            {
                respuesta = RespuestaLectura.Denegar(motivoTarjeta);
            }
            else
            {
                var opcionesActuales = opciones.CurrentValue;
                var paso = torniquete.AutorizarPaso(
                    ++_ultimoPasoId,
                    ahora,
                    TimeSpan.FromSeconds(opcionesActuales.ExpiracionPasoSegundos),
                    TimeSpan.FromMilliseconds(opcionesActuales.GraciaVisionMs));
                respuesta = RespuestaLectura.Abrir(paso.Id, SaldoFijo);
            }

            _lecturas[(torniqueteId, lecturaId)] = respuesta;
            return respuesta;
        }
    }

    public RespuestaLatido Latido(string torniqueteId)
    {
        lock (_cerrojo)
        {
            var torniquete = _torniquetes[torniqueteId];
            torniquete.ActualizarPorTiempo(Ahora());

            return new RespuestaLatido(
                BrazoDeseado: torniquete.BrazoDeseadoAbierto ? "abierto" : "cerrado",
                SirenaDeseada: torniquete.SirenaDeseada,
                PasoId: torniquete.PasoPendiente?.Id,
                ModoRegistro: torniquete.Estado == EstadoTorniquete.ModoRegistro,
                Desafio: DesafioFijo);
        }
    }

    public RespuestaCruce RegistrarCruce(
        string torniqueteId, string sesionVision, long trackId, SentidoCruce sentido, DateTime horaFotograma)
    {
        lock (_cerrojo)
        {
            // RF-26 CA3: sesionVision + trackId es la clave de idempotencia.
            if (!_cruces.Add((torniqueteId, sesionVision, trackId)))
                return RespuestaCruce.Duplicado;

            var resultado = _torniquetes[torniqueteId].RegistrarCruce(sentido, horaFotograma, Ahora());

            return resultado.Tipo switch
            {
                TipoResultadoCruce.Consumido => RespuestaCruce.Consumido(resultado.Paso!.Id),
                TipoResultadoCruce.Fraude => RespuestaCruce.Fraude(
                    resultado.AlertaNueva ? CrearAlerta(torniqueteId) : AlertaAbierta(torniqueteId)),
                _ => RespuestaCruce.Registrado,
            };
        }
    }

    /// <summary>Devuelve false si la alerta no existe. En el MVP, apagar la alarma también cierra la alerta.</summary>
    public bool ApagarAlarma(string alertaId)
    {
        lock (_cerrojo)
        {
            if (!_alertas.TryGetValue(alertaId, out var alerta))
                return false;

            if (alerta.Abierta)
            {
                _torniquetes[alerta.TorniqueteId].ApagarAlarma();
                alerta.Abierta = false;
            }

            return true;
        }
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;

    private string CrearAlerta(string torniqueteId)
    {
        var alertaId = $"al_{++_ultimaAlertaId}";
        _alertas[alertaId] = new AlertaEnMemoria(torniqueteId);
        return alertaId;
    }

    private string AlertaAbierta(string torniqueteId) =>
        _alertas.First(par => par.Value.Abierta && par.Value.TorniqueteId == torniqueteId).Key;

    private sealed class AlertaEnMemoria(string torniqueteId)
    {
        public string TorniqueteId { get; } = torniqueteId;

        public bool Abierta { get; set; } = true;
    }
}
