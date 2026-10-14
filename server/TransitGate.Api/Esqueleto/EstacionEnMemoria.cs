using System.Collections.Concurrent;
using TransitGate.Api.Contratos;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Esqueleto;

/// <summary>
/// Lo que queda del esqueleto de la semana 1: cruces y alertas en memoria, sobre el mismo torniquete que
/// usan ValidarLectura y ProcesarLatido. Pasan a casos de uso de Application en las semanas 4 (cruces)
/// y 5 (AlertaSeguridad).
/// </summary>
public sealed class EstacionEnMemoria(
    ITorniqueteRepositorio torniquetes,
    CerrojoTorniquetes cerrojo,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    private readonly ConcurrentDictionary<(string Torniquete, string SesionVision, long TrackId), byte> _cruces = new();
    private readonly ConcurrentDictionary<string, AlertaEnMemoria> _alertas = new();
    private int _ultimaAlertaId;

    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<RespuestaCruce?> RegistrarCruceAsync(
        string torniqueteId,
        string sesionVision,
        long trackId,
        SentidoCruce sentido,
        DateTime horaFotograma,
        CancellationToken ct = default)
    {
        var llegada = Ahora();
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            var respuesta = Evaluar(torniquete, sesionVision, trackId, sentido, horaFotograma);
            bitacora.Cruce(torniqueteId, trackId, sentido, horaFotograma, llegada, respuesta.Resultado);
            return respuesta;
        }
    }

    /// <summary>Devuelve false si la alerta no existe. En el MVP, apagar la alarma también cierra la alerta.</summary>
    public async Task<bool> ApagarAlarmaAsync(string alertaId, CancellationToken ct = default)
    {
        if (!_alertas.TryGetValue(alertaId, out var alerta))
            return false;

        using (await cerrojo.TomarAsync(alerta.TorniqueteId, ct))
        {
            if (alerta.Abierta)
            {
                var torniquete = await torniquetes.ObtenerAsync(alerta.TorniqueteId, ct);
                var antes = torniquete!.Deseado;
                torniquete.ApagarAlarma(Ahora());
                alerta.Abierta = false;
                bitacora.SiCambioElDeseado(torniquete, antes, "apagar-alarma");
            }
        }

        return true;
    }

    private RespuestaCruce Evaluar(
        Torniquete torniquete, string sesionVision, long trackId, SentidoCruce sentido, DateTime horaFotograma)
    {
        // RF-26 CA3: sesionVision + trackId es la clave de idempotencia.
        if (!_cruces.TryAdd((torniquete.Id, sesionVision, trackId), 0))
            return RespuestaCruce.Duplicado;

        var antes = torniquete.Deseado;
        var resultado = torniquete.RegistrarCruce(sentido, horaFotograma, Ahora());
        bitacora.SiCambioElDeseado(torniquete, antes, "cruce");

        return resultado.Tipo switch
        {
            TipoResultadoCruce.Consumido => RespuestaCruce.Consumido(resultado.Paso!.Id),
            TipoResultadoCruce.Fraude => RespuestaCruce.Fraude(
                resultado.AlertaNueva ? CrearAlerta(torniquete.Id) : AlertaAbierta(torniquete.Id)),
            _ => RespuestaCruce.Registrado,
        };
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;

    private string CrearAlerta(string torniqueteId)
    {
        var alertaId = $"al_{Interlocked.Increment(ref _ultimaAlertaId)}";
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
