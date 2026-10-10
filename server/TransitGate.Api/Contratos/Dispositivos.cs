using System.Text.Json.Serialization;
using TransitGate.Domain.Accesos;

namespace TransitGate.Api.Contratos;

// JSON del Contrato API v1.1: §6 (ESP32 principal) y §7 (servicio de visión).
// Los campos de las solicitudes son anulables para responder 400 con un mensaje claro si faltan (RNF-24).

public record SolicitudLectura(string? LecturaId, string? Tipo, string? Valor);

public record RespuestaLectura(
    string Accion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? PasoId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SaldoRestante = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Motivo = null)
{
    public static RespuestaLectura Abrir(long pasoId, int saldoRestante) => new("abrir", pasoId, saldoRestante);

    public static RespuestaLectura Denegar(MotivoIntento motivo) => new("denegar", Motivo: motivo.ToString());
}

public record SolicitudLatido(string? BrazoReal, bool? SirenaReal, long? PasoIdAbierto, string? Arranque, string? Fw);

public record RespuestaLatido(
    string BrazoDeseado,
    bool SirenaDeseada,
    long? PasoId,
    bool ModoRegistro,
    DesafioLatido? Desafio);

public record DesafioLatido(string DesafioId, string Valor, int ExpiraEn);

public record SolicitudCruce(long? TrackId, string? SesionVision, string? Sentido, DateTime? HoraFotograma);

public record RespuestaCruce(
    string Resultado,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? PasoId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AlertaId = null)
{
    public static readonly RespuestaCruce Registrado = new("registrado");
    public static readonly RespuestaCruce Duplicado = new("duplicado");

    public static RespuestaCruce Consumido(long pasoId) => new("consumido", PasoId: pasoId);

    public static RespuestaCruce Fraude(string alertaId) => new("fraude", AlertaId: alertaId);
}

public record SolicitudLatidoVision(double? Fps, string? SesionVision);

/// <summary>Cuerpo de los errores: código estable más mensaje en español (Contrato §1, RNF-32).</summary>
public record ErrorApi(string Error, string Mensaje);
