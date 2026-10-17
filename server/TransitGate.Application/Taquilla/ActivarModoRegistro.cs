using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Taquilla;

public enum ResultadoActivarModoRegistro
{
    Activado,
    TorniqueteNoRegistrado,
    SolicitudNoEncontrada,

    /// <summary>El torniquete no está en Cerrado: tiene un paso pendiente, está en alarma o ya en modo registro (RF-09 CA4).</summary>
    TorniqueteOcupado,
}

/// <summary>
/// El operador elige una solicitud y pone el lector en modo registro (RF-09, RF-36 CA3): la siguiente lectura
/// de ese torniquete se liga a la solicitud en lugar de validarse como un paso. Dura como máximo 60 s (S6).
/// El panel ve el cambio a ModoRegistro por el monitor, como cualquier otro cambio de estado.
/// </summary>
public sealed class ActivarModoRegistro(
    ITorniqueteRepositorio torniquetes,
    ISolicitudesTarjeta solicitudes,
    IUnidadDeTrabajo unidadDeTrabajo,
    IParametrosOperacion parametros,
    CerrojoTorniquetes cerrojo,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    public async Task<ResultadoActivarModoRegistro> EjecutarAsync(
        string torniqueteId, Guid solicitudId, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return ResultadoActivarModoRegistro.TorniqueteNoRegistrado;
        if (await solicitudes.PendientePorIdAsync(solicitudId) is null)
            return ResultadoActivarModoRegistro.SolicitudNoEncontrada;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var antes = torniquete.Deseado;
            torniquete.ActualizarPorTiempo(ahora);
            bitacora.SiCambioElDeseado(torniquete, antes, "vencimiento");

            if (torniquete.Estado != EstadoTorniquete.Cerrado)
                return ResultadoActivarModoRegistro.TorniqueteOcupado;

            torniquete.ActivarModoRegistro(ahora, parametros.DuracionModoRegistro, solicitudId);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        }

        return ResultadoActivarModoRegistro.Activado;
    }
}
