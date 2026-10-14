namespace TransitGate.Application;

/// <summary>
/// Guarda en una sola operación todo lo que cambió en el caso de uso (RNF-15): con EF Core es un
/// único SaveChanges sobre el DbContext que comparten los repositorios y el ISaldoService de Jeisy.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
