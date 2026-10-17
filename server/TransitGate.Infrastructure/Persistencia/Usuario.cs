using Microsoft.AspNetCore.Identity;

namespace TransitGate.Infrastructure.Persistencia;

/// <summary>
/// Cuenta de acceso de Identity (correo y contraseña con hash y sal, RNF-17). En un cliente, su Id es el mismo
/// del Cliente, así la sesión ya dice de quién son los datos (RF-06). El personal no tiene Cliente.
/// </summary>
public class Usuario : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = "";

    /// <summary>Un operador desactivado no puede iniciar sesión (RF-02 CA4, RF-42).</summary>
    public bool Activo { get; set; } = true;
}

/// <summary>Roles de la sesión (RF-06). En el MVP, el Operador hace taquilla y seguridad (MVP §3.1).</summary>
public static class Roles
{
    public const string Cliente = "Cliente";
    public const string Operador = "Operador";
    public const string Seguridad = "Seguridad";
    public const string Administrador = "Administrador";

    public static readonly string[] Todos = [Cliente, Operador, Seguridad, Administrador];
}
