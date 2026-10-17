using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

// Contrato C7 (contratos de integración §7): lo implementa el módulo de cuentas y lo consume el de
// validación. Cambiarlo requiere PR con la etiqueta "contrato".

/// <summary>ClienteId sirve para avisarle al cliente de su paso y su saldo nuevo (RF-16 CA2, RF-39 CA4).</summary>
public record SoporteResuelto(Guid SoporteId, Guid TarjetaId, Guid ClienteId, EstadoSoporte Estado);

public interface ISoporteResolver
{
    Task<SoporteResuelto?> PorUidAsync(string uidHex);

    Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId);

    Task<bool> VerificarFirmaAsync(Guid soporteId, string torniqueteId, byte[] desafio, byte[] firma); // Fase 4
}
