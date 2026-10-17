using Microsoft.AspNetCore.Http.HttpResults;
using TransitGate.Api.Contratos;
using TransitGate.Application.Taquilla;
using TransitGate.Application.Torniquetes;

namespace TransitGate.Api.Endpoints;

/// <summary>
/// Entrega de la tarjeta física en taquilla (Contrato §4 y §8, RF-09, RF-10, RF-36). Las usa el operador;
/// todavía no piden sesión, la piden cuando exista Identity.
/// </summary>
public static class TaquillaEndpoints
{
    public static IEndpointRouteBuilder MapTaquilla(this IEndpointRouteBuilder app)
    {
        var taquilla = app.MapGroup("/api").WithTags("Taquilla");
        taquilla.MapGet("/tarjetas/solicitudes", ListarSolicitudes)
            .WithSummary("Solicitudes de tarjeta física pendientes, la más antigua primero; ?documento= para buscar una (RF-36). Sin sesión hasta que exista Identity");
        taquilla.MapPost("/torniquetes/{id}/modo-registro", ActivarModoRegistro)
            .WithSummary("Pone el lector en modo registro para entregar la tarjeta de una solicitud (RF-09, S6). Sin sesión hasta que exista Identity");

        return app;
    }

    private static async Task<Results<Ok<SolicitudResumen[]>, BadRequest<ErrorApi>>> ListarSolicitudes(
        string? estado, string? documento, ConsultarSolicitudes consultarSolicitudes)
    {
        // Por ahora la taquilla solo necesita las pendientes (RF-36 CA4).
        if (estado is not null && !estado.Equals("Solicitada", StringComparison.OrdinalIgnoreCase))
            return TypedResults.BadRequest(new ErrorApi("EstadoInvalido", "Por ahora solo se listan las solicitudes en estado Solicitada."));

        var solicitudes = await consultarSolicitudes.EjecutarAsync(documento);
        var esBusqueda = !string.IsNullOrWhiteSpace(documento);
        return TypedResults.Ok(solicitudes.Select(s => SolicitudResumen.Desde(s, documentoCompleto: esBusqueda)).ToArray());
    }

    private static async Task<Results<Ok<RespuestaModoRegistro>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> ActivarModoRegistro(
        string id, SolicitudModoRegistro cuerpo, ActivarModoRegistro activarModoRegistro,
        IParametrosOperacion parametros, CancellationToken ct)
    {
        if (!IdsPublicos.TrySolicitud(cuerpo.SolicitudId, out var solicitudId))
            return TypedResults.BadRequest(new ErrorApi("SolicitudIdInvalido", "solicitudId debe tener la forma s_ seguido del id de la solicitud."));

        return await activarModoRegistro.EjecutarAsync(id, solicitudId, ct) switch
        {
            ResultadoActivarModoRegistro.TorniqueteNoRegistrado =>
                TypedResults.NotFound(new ErrorApi("TorniqueteNoRegistrado", $"No existe el torniquete {id}.")),
            ResultadoActivarModoRegistro.SolicitudNoEncontrada =>
                TypedResults.NotFound(new ErrorApi("SolicitudNoEncontrada", "La solicitud no existe o ya se entregó.")),
            ResultadoActivarModoRegistro.TorniqueteOcupado =>
                TypedResults.Conflict(new ErrorApi("TorniqueteOcupado", "El torniquete no está disponible para modo registro.")),
            _ => TypedResults.Ok(new RespuestaModoRegistro((int)parametros.DuracionModoRegistro.TotalSeconds)),
        };
    }
}
