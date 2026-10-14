using TransitGate.Domain.Accesos;

namespace TransitGate.Application.Accesos;

public interface IIntentoRepositorio
{
    Task<long> SiguienteIdAsync(CancellationToken ct = default);

    Task AgregarAsync(IntentoAcceso intento, CancellationToken ct = default);

    /// <summary>Para responder igual a un reintento del ESP32 (RF-18 CA4).</summary>
    Task<IntentoAcceso?> BuscarPorLecturaAsync(string torniqueteId, string lecturaId, CancellationToken ct = default);

    /// <summary>Los más recientes primero (RF-32).</summary>
    Task<IReadOnlyList<IntentoAcceso>> ListarPorTorniqueteAsync(string torniqueteId, int limite, CancellationToken ct = default);
}
