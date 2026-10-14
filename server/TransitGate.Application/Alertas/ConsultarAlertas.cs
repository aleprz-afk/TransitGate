using TransitGate.Domain.Alertas;

namespace TransitGate.Application.Alertas;

/// <summary>Alertas para el panel, por ejemplo las abiertas al cargar la página (RF-33).</summary>
public sealed class ConsultarAlertas(IAlertaRepositorio alertas)
{
    public Task<IReadOnlyList<AlertaSeguridad>> EjecutarAsync(EstadoAlerta? estado, CancellationToken ct = default) =>
        alertas.ListarAsync(estado, ct);
}
