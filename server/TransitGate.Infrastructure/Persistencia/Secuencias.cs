using Npgsql;

namespace TransitGate.Infrastructure.Persistencia;

/// <summary>
/// Ids que se piden antes de guardar: el del paso se reserva antes de debitar (C7) y los demás se usan en la
/// respuesta antes del SaveChanges. Salen de secuencias de PostgreSQL, así no se repiten al reiniciar la API.
/// </summary>
public sealed class Secuencias(NpgsqlDataSource baseDeDatos)
{
    public const string Pasos = "pasos_ids";
    public const string Intentos = "intentos_ids";
    public const string Cruces = "cruces_ids";
    public const string Alertas = "alertas_ids";
    public const string Recargas = "recargas_ids";

    public static readonly string[] Todas = [Pasos, Intentos, Cruces, Alertas, Recargas];

    public async Task<long> SiguienteAsync(string secuencia, CancellationToken ct = default)
    {
        if (!Todas.Contains(secuencia))
            throw new ArgumentException($"No existe la secuencia {secuencia}.", nameof(secuencia));

        await using var comando = baseDeDatos.CreateCommand($"SELECT nextval('\"{secuencia}\"')");
        return (long)(await comando.ExecuteScalarAsync(ct))!;
    }
}
