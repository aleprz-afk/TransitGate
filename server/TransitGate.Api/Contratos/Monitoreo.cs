using System.Text.Json.Serialization;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;

namespace TransitGate.Api.Contratos;

// JSON del Contrato API v1.1, §8 (monitoreo y operación).

public record TorniqueteResumen(string Id, string Nombre, string Estado, bool Conectado, DateTime? UltimaSenal)
{
    public static TorniqueteResumen Desde(ResumenTorniquete resumen) => new(
        resumen.Id, resumen.Nombre, resumen.Estado.ToString(), resumen.Conectado, resumen.UltimaSenal);
}

public record AlertaResumen(string AlertaId, string TorniqueteId, string Estado, DateTime Fecha)
{
    public static AlertaResumen Desde(AlertaSeguridad alerta) => new(
        IdsPublicos.Alerta(alerta.Id), alerta.TorniqueteId, alerta.Estado.ToString(), alerta.Fecha);
}

/// <summary>Un intento del historial del panel (RF-32). Sin UID ni datos del cliente (RNF-27).</summary>
public record IntentoResumen(
    long IntentoId,
    string Resultado,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Motivo,
    DateTime Fecha)
{
    public static IntentoResumen Desde(IntentoAcceso intento) => new(
        intento.Id, intento.Resultado.ToString(), intento.Motivo?.ToString(), intento.Fecha);
}
