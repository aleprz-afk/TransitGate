using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Accesos;

namespace TransitGate.Application.Accesos;

/// <summary>Historial de intentos de un torniquete para el panel (RF-32). Por ahora sin filtros, como en el MVP.</summary>
public sealed class ConsultarIntentos(ITorniqueteRepositorio torniquetes, IIntentoRepositorio intentos)
{
    public const int Limite = 100;

    /// <summary>Devuelve null si el torniquete no existe.</summary>
    public async Task<IReadOnlyList<IntentoAcceso>?> EjecutarAsync(string torniqueteId, CancellationToken ct = default)
    {
        if (await torniquetes.ObtenerAsync(torniqueteId, ct) is null)
            return null;

        return await intentos.ListarPorTorniqueteAsync(torniqueteId, Limite, ct);
    }
}
