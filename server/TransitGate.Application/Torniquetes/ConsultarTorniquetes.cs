using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Torniquetes;

public sealed record ResumenTorniquete(
    string Id, string Nombre, EstadoTorniquete Estado, bool Conectado, DateTime? UltimaSenal);

/// <summary>Estado y conexión de cada torniquete para el panel (RF-31, RF-37; Contrato §8).</summary>
public sealed class ConsultarTorniquetes(
    ITorniqueteRepositorio torniquetes,
    CerrojoTorniquetes cerrojo,
    IParametrosOperacion parametros,
    BitacoraTorniquete bitacora,
    TimeProvider reloj)
{
    public async Task<IReadOnlyList<ResumenTorniquete>> EjecutarAsync(CancellationToken ct = default)
    {
        var resumenes = new List<ResumenTorniquete>();

        foreach (var torniquete in await torniquetes.ListarAsync(ct))
        {
            using (await cerrojo.TomarAsync(torniquete.Id, ct))
            {
                // Aplica los vencimientos para que el panel no muestre un paso que ya expiró.
                var ahora = reloj.GetUtcNow().UtcDateTime;
                var antes = torniquete.Deseado;
                torniquete.ActualizarPorTiempo(ahora);
                bitacora.SiCambioElDeseado(torniquete, antes, "vencimiento");

                resumenes.Add(new ResumenTorniquete(
                    torniquete.Id,
                    torniquete.Nombre,
                    torniquete.Estado,
                    torniquete.Esp32Conectado(ahora, parametros.PlazoSenalEsp32),
                    torniquete.UltimaSenalEsp32));
            }
        }

        return resumenes;
    }
}
