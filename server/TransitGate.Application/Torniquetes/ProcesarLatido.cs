using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Torniquetes;

/// <summary>Lo que el ESP32 debe aplicar, tal como sale en la respuesta del latido (Contrato §6.3).</summary>
public sealed record OrdenEsp32(EstadoDeseado Deseado, long? PasoId, bool ModoRegistro);

/// <summary>
/// Latidos de los dispositivos. El del ESP32 (cada 300 ms, S11) es a la vez señal de vida, confirmación
/// de apertura y la vía por la que recibe el estado deseado (RF-45). El de la visión llega cada 5 s (S14).
/// </summary>
public sealed class ProcesarLatido(
    ITorniqueteRepositorio torniquetes,
    CerrojoTorniquetes cerrojo,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<OrdenEsp32?> DelEsp32Async(
        string torniqueteId, EstadoDeseado real, long? pasoIdAbierto, string arranque, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var antes = torniquete.Deseado;
            var arranqueAnterior = torniquete.ArranqueEsp32;

            torniquete.ActualizarPorTiempo(ahora);
            bitacora.SiCambioElDeseado(torniquete, antes, "vencimiento");
            torniquete.RegistrarLatidoEsp32(ahora, real, pasoIdAbierto, arranque);

            if (arranqueAnterior is not null && arranqueAnterior != arranque)
                bitacora.Esp32Reiniciado(torniqueteId, arranqueAnterior, arranque);

            return new OrdenEsp32(
                torniquete.Deseado,
                torniquete.PasoPendiente?.Id,
                torniquete.Estado == EstadoTorniquete.ModoRegistro);
        }
    }

    /// <summary>Devuelve false si el torniquete no existe.</summary>
    public async Task<bool> DeLaVisionAsync(string torniqueteId, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return false;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            torniquete.RegistrarLatidoVision(reloj.GetUtcNow().UtcDateTime);
            return true;
        }
    }
}
