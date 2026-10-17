using System.Text.Json.Serialization;
using TransitGate.Application.Cuentas;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Api.Contratos;

// JSON del Contrato API: §2 (sesión), §3 (clientes), §4 (tarjetas), §5 (recargas) y §9 (historial).
// El dinero va como entero en pesos (RNF-24).

public record SolicitudLogin(string? Correo, string? Password);

public record RespuestaSesion(string Rol, string Nombre);

public record SolicitudRegistro(
    string? NombreCompleto,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? Correo,
    string? Telefono,
    string? Password,
    bool AutorizaTratamientoDatos);

public record RespuestaRegistro(string ClienteId, string TarjetaId);

public record PerfilCliente(
    string ClienteId, string NombreCompleto, string Correo, string Telefono, string TipoDocumento, string NumeroDocumento);

public record CambioPerfil(string? Telefono);

public record SoporteJson(string Tipo, string Estado)
{
    public static SoporteJson? Desde(SoporteVista? soporte) => soporte is null
        ? null
        : new(soporte.Tipo == TipoSoporte.Fisico ? "fisico" : "virtual", soporte.Estado.ToString());
}

public record MiTarjetaJson(string TarjetaId, int Saldo, SoporteJson? Soporte)
{
    public static MiTarjetaJson Desde(MiTarjeta tarjeta) =>
        new(IdsPublicos.Tarjeta(tarjeta.TarjetaId), (int)tarjeta.Saldo, SoporteJson.Desde(tarjeta.Soporte));
}

public record RespuestaSolicitudFisica(string SolicitudId, string Estado);

public record RespuestaEstadoTarjeta(string Estado);

/// <summary>simularRechazo: el pago simulado se rechaza (RF-15 CA2). Para probar el caso en la demo.</summary>
public record SolicitudRecarga(int? Monto, bool SimularRechazo = false);

public record RecargaJson(string RecargaId, int Monto, bool Aprobada, int SaldoNuevo, DateTime Fecha)
{
    public static RecargaJson Desde(Recarga recarga) => new(
        IdsPublicos.Recarga(recarga.Id), (int)recarga.Monto, recarga.Aprobada, (int)recarga.SaldoResultante, recarga.Fecha);
}

/// <summary>Un viaje del historial del cliente (RF-39). tarifa y saldoRestante no vienen en un rechazado.</summary>
public record ViajeJson(
    long IntentoId,
    string Resultado,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Motivo,
    string TorniqueteNombre,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Tarifa,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SaldoRestante,
    DateTime Fecha)
{
    public static ViajeJson Desde(Viaje viaje) => new(
        viaje.Intento.Id,
        viaje.Intento.Resultado.ToString(),
        viaje.Intento.Motivo?.ToString(),
        viaje.TorniqueteNombre,
        viaje.Intento.TarifaCobrada is { } tarifa ? (int)tarifa : null,
        viaje.Intento.SaldoRestante is { } saldo ? (int)saldo : null,
        viaje.Intento.Fecha);
}
