using Microsoft.Extensions.Logging;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Torniquetes;

/// <summary>
/// Registros con horas UTC al milisegundo para las mediciones de H1: RNF-04 (cambios del estado deseado)
/// y RNF-06 (llegada de los cruces). Cada línea empieza con el RNF para filtrarla fácil en el log.
/// </summary>
public sealed partial class BitacoraTorniquete(ILogger<BitacoraTorniquete> logger)
{
    public void SiCambioElDeseado(Torniquete torniquete, EstadoDeseado antes, string causa)
    {
        var deseado = torniquete.Deseado;
        if (deseado == antes)
            return;

        EstadoDeseadoCambiado(
            torniquete.Id,
            deseado.BrazoAbierto ? "abierto" : "cerrado",
            deseado.SirenaEncendida ? "encendida" : "apagada",
            Hora(torniquete.DeseadoDesde),
            causa);
    }

    public void Lectura(string torniqueteId, string lecturaId, string uidHex, string resultado, DateTime llegada) =>
        LecturaProcesada(torniqueteId, lecturaId, uidHex[^4..], resultado, Hora(llegada));

    public void Cruce(
        string torniqueteId, long trackId, SentidoCruce sentido, DateTime horaFotograma, DateTime llegada, string resultado) =>
        CruceRecibido(
            torniqueteId,
            trackId,
            sentido,
            Hora(horaFotograma),
            Hora(llegada),
            (long)(llegada - horaFotograma).TotalMilliseconds,
            resultado);

    [LoggerMessage(LogLevel.Information,
        "RNF-04 {TorniqueteId} estado deseado: brazo={Brazo} sirena={Sirena} desde={Desde} causa={Causa}")]
    private partial void EstadoDeseadoCambiado(string torniqueteId, string brazo, string sirena, string desde, string causa);

    [LoggerMessage(LogLevel.Information,
        "Lectura {TorniqueteId} {LecturaId} uid=…{UidFinal} resultado={Resultado} llegada={Llegada}")]
    private partial void LecturaProcesada(string torniqueteId, string lecturaId, string uidFinal, string resultado, string llegada);

    [LoggerMessage(LogLevel.Information,
        "RNF-06 {TorniqueteId} cruce trackId={TrackId} sentido={Sentido} fotograma={Fotograma} llegada={Llegada} retrasoMs={RetrasoMs} resultado={Resultado}")]
    private partial void CruceRecibido(
        string torniqueteId, long trackId, SentidoCruce sentido, string fotograma, string llegada, long retrasoMs, string resultado);

    [LoggerMessage(LogLevel.Warning, "{TorniqueteId} el ESP32 se reinició: arranque {Anterior} → {Nuevo}")]
    public partial void Esp32Reiniciado(string torniqueteId, string anterior, string nuevo);

    private static string Hora(DateTime? hora) => hora?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") ?? "-";
}
