using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

// Contrato C7 (contratos de integración §7): lo implementa el módulo de cuentas (Jeisy)
// y lo consume el de validación (Alex). Cambiarlo requiere PR con la etiqueta "contrato".

public record SoporteResuelto(Guid SoporteId, Guid TarjetaId, EstadoSoporte Estado);

public interface ISoporteResolver
{
    Task<SoporteResuelto?> PorUidAsync(string uidHex);

    Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId);

    Task<bool> VerificarFirmaAsync(Guid soporteId, string torniqueteId, byte[] desafio, byte[] firma); // Fase 4
}
