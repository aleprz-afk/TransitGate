namespace TransitGate.Application.Cuentas;

// Contrato C7 (contratos de integración §7). DebitarAsync no abre su propia transacción:
// participa en la del caso de uso de validación (RNF-15) y controla la concurrencia del saldo (RNF-16).

/// <summary>Si el saldo no alcanza, Exitoso es false y el saldo no cambia (S3: no hay saldo negativo).</summary>
public record ResultadoDebito(bool Exitoso, decimal SaldoRestante);

public interface ISaldoService
{
    Task<decimal> TarifaVigenteAsync();

    Task<ResultadoDebito> DebitarAsync(Guid tarjetaId, decimal monto, long pasoId);

    Task RevertirAsync(long pasoId); // RF-46
}
