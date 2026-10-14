using TransitGate.Domain.Alertas;

namespace TransitGate.Application.Alertas;

public interface IAlertaRepositorio
{
    Task<long> SiguienteIdAsync(CancellationToken ct = default);

    Task AgregarAsync(AlertaSeguridad alerta, CancellationToken ct = default);

    Task<AlertaSeguridad?> ObtenerAsync(long alertaId, CancellationToken ct = default);

    /// <summary>La alerta abierta de un torniquete, si hay (RF-28 CA3).</summary>
    Task<AlertaSeguridad?> AbiertaDeAsync(string torniqueteId, CancellationToken ct = default);

    /// <summary>Las más recientes primero; todas si <paramref name="estado"/> es null (RF-33).</summary>
    Task<IReadOnlyList<AlertaSeguridad>> ListarAsync(EstadoAlerta? estado, CancellationToken ct = default);
}
