using System.Diagnostics;
using System.Globalization;

namespace TransitGate.Api.Diagnostico;

/// <summary>
/// Registra cuánto tarda cada petición a /api, desde que llega hasta que sale la respuesta (RNF-03).
/// Los latidos llegan cada 300 ms, así que van en nivel Debug salvo que fallen o pasen de 200 ms.
/// </summary>
public sealed partial class MiddlewareTiempos(RequestDelegate siguiente, ILogger<MiddlewareTiempos> logger)
{
    // RNF-03: p95 ≤ 200 ms para decidir un paso.
    private static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(200);

    public async Task InvokeAsync(HttpContext contexto)
    {
        var ruta = contexto.Request.Path;
        if (!ruta.StartsWithSegments("/api"))
        {
            await siguiente(contexto);
            return;
        }

        var inicio = Stopwatch.GetTimestamp();
        var estado = StatusCodes.Status500InternalServerError;
        try
        {
            await siguiente(contexto);
            estado = contexto.Response.StatusCode;
        }
        finally
        {
            // Si hubo una excepción, todavía no se ha escrito el 500: se registra como 500.
            var duracion = Stopwatch.GetElapsedTime(inicio);
            var nivel = estado >= 500 || duracion > Limite ? LogLevel.Warning
                : EsLatido(ruta) ? LogLevel.Debug
                : LogLevel.Information;

            // Con punto decimal siempre, para que el log se pueda analizar como CSV sin importar el idioma del equipo.
            var milisegundos = duracion.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);
            Peticion(nivel, contexto.Request.Method, ruta.Value!, estado, milisegundos);
        }
    }

    private static bool EsLatido(PathString ruta) => ruta.Value!.EndsWith("/latido", StringComparison.Ordinal);

    [LoggerMessage("RNF-03 {Metodo} {Ruta} {Estado} {DuracionMs} ms")]
    private partial void Peticion(LogLevel nivel, string metodo, string ruta, int estado, string duracionMs);
}
