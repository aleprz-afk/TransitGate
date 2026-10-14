using Microsoft.Extensions.Options;
using TransitGate.Application.Dispositivos;

namespace TransitGate.Api.Seguridad;

/// <summary>
/// Credenciales de los dispositivos (RNF-20). Las claves van en los secretos de usuario, nunca en
/// appsettings.json ni en el repositorio (RNF-25):
/// <c>dotnet user-secrets set "Dispositivos:Claves:tq_1:Esp32" "&lt;clave&gt;"</c>.
/// </summary>
public sealed class OpcionesDispositivos
{
    public const string Seccion = "Dispositivos";

    /// <summary>
    /// En false (modo tolerante) las peticiones sin clave o con una clave mala se aceptan y solo quedan
    /// en el log; en true reciben 401.
    /// </summary>
    public bool ExigirClave { get; set; }

    public Dictionary<string, ClavesTorniquete> Claves { get; set; } = [];
}

public sealed class ClavesTorniquete
{
    public string? Esp32 { get; set; }

    public string? Vision { get; set; }
}

public sealed class ClavesDesdeConfiguracion(IOptionsMonitor<OpcionesDispositivos> opciones) : IClavesDispositivos
{
    public string? ClaveDe(string torniqueteId, TipoDispositivo dispositivo)
    {
        if (!opciones.CurrentValue.Claves.TryGetValue(torniqueteId, out var claves))
            return null;

        return dispositivo == TipoDispositivo.Esp32 ? claves.Esp32 : claves.Vision;
    }
}
