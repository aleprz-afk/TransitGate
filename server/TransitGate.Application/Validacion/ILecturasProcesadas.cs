namespace TransitGate.Application.Validacion;

/// <summary>
/// Respuestas ya dadas por lecturaId, para que un reintento del ESP32 no cobre dos veces (RF-18 CA4).
/// Desde la semana 3 la respuesta sale del IntentoAcceso guardado con ese lecturaId.
/// </summary>
public interface ILecturasProcesadas
{
    Task<ResultadoLectura?> BuscarAsync(string torniqueteId, string lecturaId, CancellationToken ct = default);

    Task GuardarAsync(string torniqueteId, string lecturaId, ResultadoLectura resultado, CancellationToken ct = default);
}
