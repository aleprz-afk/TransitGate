using System.Text.RegularExpressions;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Application.Cuentas;

public sealed record DatosRegistro(
    string? NombreCompleto,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? Correo,
    string? Telefono,
    string? Password,
    bool AutorizaTratamientoDatos);

public sealed record ClienteRegistrado(Guid ClienteId, Guid TarjetaId);

/// <summary>
/// Registro en el portal (RF-01): crea el Cliente, su Tarjeta con saldo 0 y sin soportes (RF-07) y su cuenta
/// de acceso. Si algo no es válido, no se crea nada.
/// </summary>
public sealed partial class RegistrarCliente(
    IClienteRepositorio clientes,
    ITarjetaRepositorio tarjetas,
    ICuentasAcceso cuentasAcceso,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    public const int LargoMinimoPassword = 8;

    private static readonly string[] TiposDocumento = ["CC", "CE", "TI", "PP"];

    public async Task<Resultado<ClienteRegistrado>> EjecutarAsync(DatosRegistro datos, CancellationToken ct = default)
    {
        if (Validar(datos) is { } invalido)
            return invalido;

        var correo = datos.Correo!.Trim().ToLowerInvariant();
        var documento = datos.NumeroDocumento!.Trim();
        if (await clientes.ExisteCorreoAsync(correo, ct))
            return ErrorCuenta.Invalido("CorreoDuplicado", "Ya existe una cuenta con ese correo.");
        if (await clientes.ExisteDocumentoAsync(documento, ct))
            return ErrorCuenta.Invalido("DocumentoDuplicado", "Ya existe una cuenta con ese documento.");

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var cliente = new Cliente(
            Guid.NewGuid(), datos.NombreCompleto!.Trim(), datos.TipoDocumento!.Trim().ToUpperInvariant(),
            documento, correo, datos.Telefono!.Trim(), ahora);
        var tarjeta = new Tarjeta(Guid.NewGuid(), cliente.Id);

        if (await cuentasAcceso.CrearClienteAsync(cliente.Id, correo, datos.Password!, ct) is { } errorAcceso)
            return errorAcceso;

        await clientes.AgregarAsync(cliente, ct);
        await tarjetas.AgregarAsync(tarjeta, ct);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return new ClienteRegistrado(cliente.Id, tarjeta.Id);
    }

    // RF-01 CA3 a CA5: cada campo con su código, para que el portal marque el que falla.
    private static ErrorCuenta? Validar(DatosRegistro datos)
    {
        if (!datos.AutorizaTratamientoDatos)
            return ErrorCuenta.Invalido("AutorizacionRequerida", "Debes autorizar el tratamiento de datos.");
        if (string.IsNullOrWhiteSpace(datos.NombreCompleto))
            return ErrorCuenta.Invalido("NombreRequerido", "Escribe tu nombre completo.");

        var tipo = datos.TipoDocumento?.Trim().ToUpperInvariant();
        if (tipo is null || !TiposDocumento.Contains(tipo))
            return ErrorCuenta.Invalido("TipoDocumentoInvalido", "El tipo de documento debe ser CC, CE, TI o PP.");

        // Solo el pasaporte puede tener letras.
        var documento = datos.NumeroDocumento?.Trim() ?? "";
        if (!(tipo == "PP" ? Pasaporte() : SoloDigitos()).IsMatch(documento))
            return ErrorCuenta.Invalido("DocumentoInvalido", "El número de documento no es válido.");
        if (datos.Correo is null || !Correo().IsMatch(datos.Correo.Trim()))
            return ErrorCuenta.Invalido("CorreoInvalido", "El correo no es válido.");
        if (datos.Telefono is null || !Celular().IsMatch(datos.Telefono.Trim()))
            return ErrorCuenta.Invalido("TelefonoInvalido", "El teléfono debe tener 10 dígitos.");
        if (datos.Password is null || datos.Password.Length < LargoMinimoPassword)
            return ErrorCuenta.Invalido("PasswordCorta", "La contraseña debe tener al menos 8 caracteres.");

        return null;
    }

    [GeneratedRegex("^[0-9]{5,12}$")]
    private static partial Regex SoloDigitos();

    [GeneratedRegex("^[A-Za-z0-9]{5,12}$")]
    private static partial Regex Pasaporte();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Correo();

    [GeneratedRegex("^[0-9]{10}$")]
    private static partial Regex Celular();
}
