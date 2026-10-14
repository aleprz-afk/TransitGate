using Microsoft.Extensions.Options;
using TransitGate.Api.Contratos;
using TransitGate.Application.Dispositivos;

namespace TransitGate.Api.Seguridad;

/// <summary>
/// Exige la cabecera X-Device-Key en las rutas de un dispositivo (Contrato §2, RNF-20).
/// Con ExigirClave en false solo verifica y deja constancia en el log.
/// </summary>
public sealed class FiltroClaveDispositivo(TipoDispositivo dispositivo) : IEndpointFilter
{
    public const string Cabecera = "X-Device-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var http = contexto.HttpContext;
        var verificador = http.RequestServices.GetRequiredService<VerificadorClaveDispositivo>();
        var exigir = http.RequestServices.GetRequiredService<IOptionsMonitor<OpcionesDispositivos>>().CurrentValue.ExigirClave;

        var torniqueteId = http.GetRouteValue("id") as string ?? "";
        var resultado = verificador.Verificar(torniqueteId, dispositivo, http.Request.Headers[Cabecera]);

        if (resultado != ResultadoClave.Valida && exigir)
        {
            return TypedResults.Json(
                new ErrorApi("CredencialInvalida", $"Falta la cabecera {Cabecera} o no corresponde a este torniquete."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await siguiente(contexto);
    }
}

public static class FiltroClaveDispositivoExtensiones
{
    public static RouteGroupBuilder ExigirClaveDe(this RouteGroupBuilder grupo, TipoDispositivo dispositivo)
    {
        grupo.AddEndpointFilter(new FiltroClaveDispositivo(dispositivo));
        grupo.WithMetadata(new ProducesResponseTypeMetadata(
            StatusCodes.Status401Unauthorized, typeof(ErrorApi), ["application/json"]));
        return grupo;
    }
}
