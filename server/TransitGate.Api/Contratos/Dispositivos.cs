using System.Text.Json.Serialization;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Api.Contratos;

// JSON del Contrato API v1.1: §6 (ESP32 principal) y §7 (servicio de visión).
// Los campos de las solicitudes son anulables para responder 400 con un mensaje claro si faltan (RNF-24).

public record SolicitudLectura(string? LecturaId, string? Tipo, string? Valor);

public record RespuestaLectura(
    string Accion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? PasoId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SaldoRestante = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Motivo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LecturaId = null)
{
    public static RespuestaLectura Desde(ResultadoLectura resultado, string lecturaId) => resultado.Accion switch
    {
        AccionLectura.Abrir => new("abrir", resultado.PasoId, (int)resultado.SaldoRestante!.Value),
        AccionLectura.Registrada => new("registrada", LecturaId: lecturaId),   // Contrato §4: el brazo no se mueve
        _ => new("denegar", Motivo: resultado.Motivo),
    };
}

public record SolicitudLatido(string? BrazoReal, bool? SirenaReal, long? PasoIdAbierto, string? Arranque, string? Fw);

public record RespuestaLatido(
    string BrazoDeseado,
    bool SirenaDeseada,
    long? PasoId,
    bool ModoRegistro,
    DesafioLatido? Desafio)
{
    // El celular es Fase 4: por ahora va siempre el desafío del vector de prueba de C2 (16 bytes en 0).
    private static readonly DesafioLatido DesafioFijo = new("d_esqueleto", new string('0', 32), 30);

    public static RespuestaLatido Desde(OrdenEsp32 orden) => new(
        orden.Deseado.BrazoAbierto ? "abierto" : "cerrado",
        orden.Deseado.SirenaEncendida,
        orden.PasoId,
        orden.ModoRegistro,
        DesafioFijo);
}

public record DesafioLatido(string DesafioId, string Valor, int ExpiraEn);

public record SolicitudCruce(long? TrackId, string? SesionVision, string? Sentido, DateTime? HoraFotograma);

public record RespuestaCruce(
    string Resultado,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? PasoId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AlertaId = null)
{
    public static RespuestaCruce Desde(ResultadoRegistroCruce registro) => registro.Cruce switch
    {
        null => new("duplicado"),
        { Resultado: TipoResultadoCruce.Consumido } cruce => new("consumido", PasoId: cruce.PasoId),
        { Resultado: TipoResultadoCruce.Fraude } cruce => new("fraude", AlertaId: IdsPublicos.Alerta(cruce.AlertaId!.Value)),
        _ => new("registrado"),
    };
}

public record SolicitudLatidoVision(double? Fps, string? SesionVision);

/// <summary>Cuerpo de los errores: código estable más mensaje en español (Contrato §1, RNF-32).</summary>
public record ErrorApi(string Error, string Mensaje);
