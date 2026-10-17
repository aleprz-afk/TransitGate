namespace TransitGate.Api.Contratos;

/// <summary>
/// Formato de los ids hacia afuera. El contrato muestra las alertas como "al_9" y las solicitudes como "s_…";
/// adentro las alertas son números y las solicitudes, el Guid del SoporteTarjeta.
/// </summary>
public static class IdsPublicos
{
    private const string PrefijoAlerta = "al_";
    private const string PrefijoSolicitud = "s_";

    public static string Alerta(long id) => $"{PrefijoAlerta}{id}";

    public static bool TryAlerta(string texto, out long id)
    {
        id = 0;
        return texto.StartsWith(PrefijoAlerta, StringComparison.Ordinal)
            && long.TryParse(texto.AsSpan(PrefijoAlerta.Length), out id);
    }

    public static string Solicitud(Guid id) => $"{PrefijoSolicitud}{id:N}";

    public static string Cliente(Guid id) => $"c_{id:N}";

    public static string Tarjeta(Guid id) => $"t_{id:N}";

    public static string Recarga(long id) => $"r_{id}";

    public static bool TrySolicitud(string? texto, out Guid id)
    {
        id = Guid.Empty;
        return texto is not null
            && texto.StartsWith(PrefijoSolicitud, StringComparison.Ordinal)
            && Guid.TryParseExact(texto.AsSpan(PrefijoSolicitud.Length), "N", out id);
    }
}
