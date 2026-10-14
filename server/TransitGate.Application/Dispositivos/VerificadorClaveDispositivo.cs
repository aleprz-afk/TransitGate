using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TransitGate.Application.Dispositivos;

public enum ResultadoClave
{
    Valida,
    Ausente,
    Invalida,
    SinConfigurar,
}

/// <summary>
/// Comprueba la cabecera X-Device-Key (RNF-20). Solo registra en el log cuando el resultado de un
/// dispositivo cambia, porque el ESP32 envía un latido cada 300 ms y el log se llenaría.
/// </summary>
public sealed partial class VerificadorClaveDispositivo(
    IClavesDispositivos claves,
    ILogger<VerificadorClaveDispositivo> logger)
{
    private readonly ConcurrentDictionary<(string Torniquete, TipoDispositivo Dispositivo), ResultadoClave> _ultimos = new();

    public ResultadoClave Verificar(string torniqueteId, TipoDispositivo dispositivo, string? clavePresentada)
    {
        var resultado = Comparar(claves.ClaveDe(torniqueteId, dispositivo), clavePresentada);

        var anterior = _ultimos.GetValueOrDefault((torniqueteId, dispositivo), ResultadoClave.Valida);
        _ultimos[(torniqueteId, dispositivo)] = resultado;
        if (resultado != anterior)
        {
            if (resultado == ResultadoClave.Valida)
                ClaveCorregida(torniqueteId, dispositivo);
            else
                ClaveConProblema(torniqueteId, dispositivo, resultado);
        }

        return resultado;
    }

    private static ResultadoClave Comparar(string? esperada, string? presentada)
    {
        if (string.IsNullOrEmpty(esperada))
            return ResultadoClave.SinConfigurar;
        if (string.IsNullOrEmpty(presentada))
            return ResultadoClave.Ausente;

        // Comparación de tiempo constante, para no dar pistas sobre la clave por lo que tarda la respuesta.
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperada), Encoding.UTF8.GetBytes(presentada))
            ? ResultadoClave.Valida
            : ResultadoClave.Invalida;
    }

    [LoggerMessage(LogLevel.Warning, "X-Device-Key {TorniqueteId} {Dispositivo}: {Resultado}")]
    private partial void ClaveConProblema(string torniqueteId, TipoDispositivo dispositivo, ResultadoClave resultado);

    [LoggerMessage(LogLevel.Information, "X-Device-Key {TorniqueteId} {Dispositivo}: válida otra vez")]
    private partial void ClaveCorregida(string torniqueteId, TipoDispositivo dispositivo);
}
