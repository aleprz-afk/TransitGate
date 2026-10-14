using TransitGate.Application.Accesos;
using TransitGate.Application.Alertas;
using TransitGate.Application.Notificaciones;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Torniquetes;

/// <summary>Lo que se le responde a la visión: el cruce guardado, o null si era un duplicado.</summary>
public sealed record ResultadoRegistroCruce(Cruce? Cruce)
{
    public bool Duplicado => Cruce is null;
}

/// <summary>
/// Caso de uso de un cruce reportado por la visión (RF-26 a RF-29). Cada cruce queda registrado (RF-26 CA1);
/// un cruce de entrada sin paso disponible deja un IntentoAcceso de fraude y abre una AlertaSeguridad, o se
/// liga a la que ya está abierta (RF-28).
/// </summary>
public sealed class RegistrarCruce(
    ITorniqueteRepositorio torniquetes,
    ICruceRepositorio cruces,
    IAlertaRepositorio alertas,
    IIntentoRepositorio intentos,
    IUnidadDeTrabajo unidadDeTrabajo,
    INotificador notificador,
    CerrojoTorniquetes cerrojo,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<ResultadoRegistroCruce?> EjecutarAsync(
        string torniqueteId,
        string sesionVision,
        long trackId,
        SentidoCruce sentido,
        DateTime horaFotograma,
        CancellationToken ct = default)
    {
        var llegada = reloj.GetUtcNow().UtcDateTime;
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is not { } torniquete)
            return null;

        Cruce cruce;
        IntentoAcceso? fraude = null;
        AlertaSeguridad? alertaNueva = null;

        using (await cerrojo.TomarAsync(torniqueteId, ct))
        {
            // RF-26 CA3: sesionVision + trackId es la clave de idempotencia.
            if (await cruces.BuscarAsync(torniqueteId, sesionVision, trackId, ct) is not null)
            {
                bitacora.Cruce(torniqueteId, trackId, sentido, horaFotograma, llegada, "duplicado");
                return new ResultadoRegistroCruce(null);
            }

            var antes = torniquete.Deseado;
            var resultado = torniquete.RegistrarCruce(sentido, horaFotograma, llegada);

            AlertaSeguridad? alerta = null;
            if (resultado.Tipo == TipoResultadoCruce.Fraude)
            {
                // Si el torniquete ya estaba en Alarma, el cruce se suma a la alerta abierta (RF-28 CA3).
                alerta = resultado.AlertaNueva ? null : await alertas.AbiertaDeAsync(torniqueteId, ct);
                if (alerta is null)
                {
                    alerta = alertaNueva = new AlertaSeguridad(await alertas.SiguienteIdAsync(ct), torniqueteId, horaFotograma);
                    await alertas.AgregarAsync(alerta, ct);
                }

                fraude = IntentoAcceso.Fraude(await intentos.SiguienteIdAsync(ct), torniqueteId, horaFotograma, alerta.Id);
                await intentos.AgregarAsync(fraude, ct);
            }

            cruce = new Cruce(
                await cruces.SiguienteIdAsync(ct), torniqueteId, sesionVision, trackId,
                sentido, horaFotograma, llegada, resultado, alerta?.Id);
            await cruces.AgregarAsync(cruce, ct);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);

            bitacora.SiCambioElDeseado(torniquete, antes, "cruce");
            bitacora.Cruce(torniqueteId, trackId, sentido, horaFotograma, llegada, cruce.Resultado.ToString().ToLowerInvariant());
        }

        // Fuera del cerrojo: avisar al panel no debe demorar al siguiente latido.
        if (alertaNueva is not null)
            await notificador.AlertaNuevaAsync(alertaNueva, ct);
        if (fraude is not null)
            await notificador.IntentoRegistradoAsync(fraude, ct);

        return new ResultadoRegistroCruce(cruce);
    }
}
