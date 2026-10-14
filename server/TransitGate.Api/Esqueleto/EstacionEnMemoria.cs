using System.Collections.Concurrent;
using TransitGate.Api.Contratos;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Esqueleto;

/// <summary>
/// Lo que queda del esqueleto de la semana 1: latido, cruces y alertas en memoria, sobre el mismo
/// torniquete que usa ValidarLectura. Pasan a casos de uso de Application en las semanas 2 (latido real),
/// 4 (cruces) y 5 (AlertaSeguridad).
/// </summary>
public sealed class EstacionEnMemoria(ITorniqueteRepositorio torniquetes, CerrojoTorniquetes cerrojo, TimeProvider reloj)
{
    // El celular es Fase 4: por ahora el latido trae siempre el desafío del vector de prueba de C2 (16 bytes en 0).
    private static readonly DesafioLatido DesafioFijo = new("d_esqueleto", new string('0', 32), 30);

    private readonly ConcurrentDictionary<(string Torniquete, string SesionVision, long TrackId), byte> _cruces = new();
    private readonly ConcurrentDictionary<string, AlertaEnMemoria> _alertas = new();
    private int _ultimaAlertaId;

    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<RespuestaLatido?> LatidoAsync(string torniqueteId, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            torniquete.ActualizarPorTiempo(Ahora());

            return new RespuestaLatido(
                BrazoDeseado: torniquete.BrazoDeseadoAbierto ? "abierto" : "cerrado",
                SirenaDeseada: torniquete.SirenaDeseada,
                PasoId: torniquete.PasoPendiente?.Id,
                ModoRegistro: torniquete.Estado == EstadoTorniquete.ModoRegistro,
                Desafio: DesafioFijo);
        }
    }

    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<RespuestaCruce?> RegistrarCruceAsync(
        string torniqueteId,
        string sesionVision,
        long trackId,
        SentidoCruce sentido,
        DateTime horaFotograma,
        CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            // RF-26 CA3: sesionVision + trackId es la clave de idempotencia.
            if (!_cruces.TryAdd((torniqueteId, sesionVision, trackId), 0))
                return RespuestaCruce.Duplicado;

            var resultado = torniquete.RegistrarCruce(sentido, horaFotograma, Ahora());

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
    public async Task<bool> ApagarAlarmaAsync(string alertaId, CancellationToken ct = default)
    {
        if (!_alertas.TryGetValue(alertaId, out var alerta))
            return false;

        using (await cerrojo.TomarAsync(alerta.TorniqueteId, ct))
        {
            if (alerta.Abierta)
            {
                var torniquete = await torniquetes.ObtenerAsync(alerta.TorniqueteId, ct);
                torniquete!.ApagarAlarma();
                alerta.Abierta = false;
            }
        }

        return true;
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
