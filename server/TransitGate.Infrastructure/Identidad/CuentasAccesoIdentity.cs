using Microsoft.AspNetCore.Identity;
using TransitGate.Application.Cuentas;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Infrastructure.Identidad;

/// <summary>
/// Crea la cuenta de acceso del cliente con Identity (contraseña con hash y sal, RNF-17), con el mismo Id del
/// Cliente. Identity guarda por su cuenta, así que esto abre una transacción que confirma el GuardarCambiosAsync
/// de RegistrarCliente: si algo falla antes, no queda una cuenta sin cliente (RNF-15).
/// </summary>
public sealed class CuentasAccesoIdentity(UserManager<Usuario> usuarios, TransitGateDbContext db) : ICuentasAcceso
{
    public async Task<ErrorCuenta?> CrearClienteAsync(Guid clienteId, string correo, string password, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            await db.Database.BeginTransactionAsync(ct);

        var usuario = new Usuario { Id = clienteId, UserName = correo, Email = correo };
        var creado = await usuarios.CreateAsync(usuario, password);
        if (!creado.Succeeded)
            return Traducir(creado);

        var conRol = await usuarios.AddToRoleAsync(usuario, Roles.Cliente);
        return conRol.Succeeded ? null : Traducir(conRol);
    }

    private static ErrorCuenta Traducir(IdentityResult resultado) =>
        resultado.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail))
            ? ErrorCuenta.Invalido("CorreoDuplicado", "Ya existe una cuenta con ese correo.")
            : ErrorCuenta.Invalido("PasswordInvalida", "La contraseña no cumple las reglas: al menos 8 caracteres.");
}
