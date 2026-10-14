using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using TransitGate.Api.Contratos;
using TransitGate.Api.Esqueleto;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Endpoints;

/// <summary>
/// Rutas de los dispositivos: ESP32 principal (Contrato §6) y servicio de visión (§7), más las dos de
/// monitoreo (§8) que hacen falta para probar el flujo completo con los simuladores (RNF-38).
/// Todavía no exigen X-Device-Key (RNF-20) ni sesión; se agregan con la base de datos e Identity.
/// </summary>
public static partial class DispositivosEndpoints
{
    public static IEndpointRouteBuilder MapDispositivos(this IEndpointRouteBuilder app)
    {
        var esp32 = app.MapGroup("/api/torniquetes/{id}").WithTags("ESP32");
        esp32.MapPost("/lecturas", RecibirLectura)
            .WithSummary("Lectura de una tarjeta física (RF-18, RF-20, RF-21, RF-22)");
        esp32.MapPost("/latido", Latido)
            .WithSummary("Latido cada 300 ms: devuelve el estado deseado (RF-45)");

        var vision = app.MapGroup("/api/torniquetes/{id}").WithTags("Visión");
        vision.MapPost("/cruces", RegistrarCruce)
            .WithSummary("Cruce de la línea virtual (RF-26 a RF-29)");
        vision.MapPost("/vision/latido", LatidoVision)
            .WithSummary("Señal de vida del servicio de visión, cada 5 s (S14)");

        var monitoreo = app.MapGroup("/api").WithTags("Monitoreo");
        monitoreo.MapGet("/torniquetes", ListarTorniquetes)
            .WithSummary("Estado y conexión de los torniquetes (RF-31, RF-37). Sin sesión hasta que exista Identity");
        monitoreo.MapPost("/alertas/{id}/apagar-alarma", ApagarAlarma)
            .WithSummary("Apaga la alarma y cierra la alerta (RF-34). Sin sesión hasta que exista Identity");

        return app;
    }

    private static async Task<Results<Ok<RespuestaLectura>, BadRequest<ErrorApi>, NotFound<ErrorApi>>> RecibirLectura(
        string id, SolicitudLectura lectura, ValidarLectura validarLectura, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(lectura.LecturaId))
            return Invalido("LecturaIdRequerido", "Falta el lecturaId.");
        if (lectura.Tipo != "uid")
            return Invalido("TipoNoSoportado", "Por ahora solo se aceptan lecturas de tipo \"uid\"; el celular llega en la Fase 4.");
        if (lectura.Valor is null || !UidHex().IsMatch(lectura.Valor))
            return Invalido("UidInvalido", "El UID va en hexadecimal mayúsculas, con 8 o 14 caracteres (RNF-35).");

        var resultado = await validarLectura.EjecutarAsync(id, lectura.LecturaId, lectura.Valor, ct);
        if (resultado is null)
            return TorniqueteNoRegistrado(id);

        return TypedResults.Ok(RespuestaLectura.Desde(resultado));
    }

    private static async Task<Results<Ok<RespuestaLatido>, BadRequest<ErrorApi>, NotFound<ErrorApi>>> Latido(
        string id, SolicitudLatido latido, ProcesarLatido procesarLatido, CancellationToken ct)
    {
        if (latido.BrazoReal is not ("abierto" or "cerrado"))
            return Invalido("BrazoRealInvalido", "brazoReal debe ser \"abierto\" o \"cerrado\".");
        if (latido.SirenaReal is null || string.IsNullOrWhiteSpace(latido.Arranque))
            return Invalido("LatidoIncompleto", "Faltan sirenaReal o arranque.");

        var real = new EstadoDeseado(latido.BrazoReal == "abierto", latido.SirenaReal.Value);
        var orden = await procesarLatido.DelEsp32Async(id, real, latido.PasoIdAbierto, latido.Arranque, ct);
        if (orden is null)
            return TorniqueteNoRegistrado(id);

        return TypedResults.Ok(RespuestaLatido.Desde(orden));
    }

    private static async Task<Results<Ok<RespuestaCruce>, BadRequest<ErrorApi>, NotFound<ErrorApi>>> RegistrarCruce(
        string id, SolicitudCruce cruce, EstacionEnMemoria estacion, CancellationToken ct)
    {
        if (cruce.TrackId is not { } trackId || string.IsNullOrWhiteSpace(cruce.SesionVision))
            return Invalido("CruceIncompleto", "Faltan trackId o sesionVision.");

        SentidoCruce? sentido = cruce.Sentido switch
        {
            "entrada" => SentidoCruce.Entrada,
            "salida" => SentidoCruce.Salida,
            _ => null,
        };
        if (sentido is null)
            return Invalido("SentidoInvalido", "sentido debe ser \"entrada\" o \"salida\".");
        if (cruce.HoraFotograma is not { Kind: DateTimeKind.Utc } horaFotograma)
            return Invalido("HoraFotogramaInvalida", "horaFotograma va en UTC, en ISO-8601 terminado en Z.");

        var respuesta = await estacion.RegistrarCruceAsync(id, cruce.SesionVision, trackId, sentido.Value, horaFotograma, ct);
        if (respuesta is null)
            return TorniqueteNoRegistrado(id);

        return TypedResults.Ok(respuesta);
    }

    private static async Task<Results<NoContent, BadRequest<ErrorApi>, NotFound<ErrorApi>>> LatidoVision(
        string id, SolicitudLatidoVision latido, ProcesarLatido procesarLatido, CancellationToken ct)
    {
        if (latido.Fps is null || string.IsNullOrWhiteSpace(latido.SesionVision))
            return Invalido("LatidoIncompleto", "Faltan fps o sesionVision.");
        if (!await procesarLatido.DeLaVisionAsync(id, ct))
            return TorniqueteNoRegistrado(id);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<TorniqueteResumen[]>> ListarTorniquetes(
        ConsultarTorniquetes consultarTorniquetes, CancellationToken ct)
    {
        var resumenes = await consultarTorniquetes.EjecutarAsync(ct);
        return TypedResults.Ok(resumenes.Select(TorniqueteResumen.Desde).ToArray());
    }

    private static async Task<Results<Ok, NotFound<ErrorApi>>> ApagarAlarma(
        string id, EstacionEnMemoria estacion, CancellationToken ct)
    {
        if (!await estacion.ApagarAlarmaAsync(id, ct))
            return TypedResults.NotFound(new ErrorApi("AlertaNoEncontrada", $"No existe la alerta {id}."));

        return TypedResults.Ok();
    }

    private static NotFound<ErrorApi> TorniqueteNoRegistrado(string id) =>
        TypedResults.NotFound(new ErrorApi("TorniqueteNoRegistrado", $"No existe el torniquete {id}."));

    private static BadRequest<ErrorApi> Invalido(string error, string mensaje) =>
        TypedResults.BadRequest(new ErrorApi(error, mensaje));

    // MIFARE Classic (4 bytes) o NTAG (7 bytes), en hexadecimal mayúsculas (RNF-35).
    [GeneratedRegex("^([0-9A-F]{8}|[0-9A-F]{14})$")]
    private static partial Regex UidHex();
}
