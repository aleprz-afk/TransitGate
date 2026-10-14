namespace TransitGate.Api.Contratos;

/// <summary>
/// Formato de los ids hacia afuera. El contrato muestra las alertas como "al_9"; adentro son números.
/// </summary>
public static class IdsPublicos
{
    private const string PrefijoAlerta = "al_";

    public static string Alerta(long id) => $"{PrefijoAlerta}{id}";

    public static bool TryAlerta(string texto, out long id)
    {
        id = 0;
        return texto.StartsWith(PrefijoAlerta, StringComparison.Ordinal)
            && long.TryParse(texto.AsSpan(PrefijoAlerta.Length), out id);
    }
}
