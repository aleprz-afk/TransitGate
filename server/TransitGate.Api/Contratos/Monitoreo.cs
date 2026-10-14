using TransitGate.Application.Torniquetes;

namespace TransitGate.Api.Contratos;

// JSON del Contrato API v1.1, §8 (monitoreo y operación).

public record TorniqueteResumen(string Id, string Nombre, string Estado, bool Conectado, DateTime? UltimaSenal)
{
    public static TorniqueteResumen Desde(ResumenTorniquete resumen) => new(
        resumen.Id, resumen.Nombre, resumen.Estado.ToString(), resumen.Conectado, resumen.UltimaSenal);
}
