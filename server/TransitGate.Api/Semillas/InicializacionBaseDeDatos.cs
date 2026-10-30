using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Api.Semillas;

/// <summary>Una cuenta del personal que existe desde el primer arranque (MVP §3.3).</summary>
public sealed class CuentaSemilla
{
    public string Correo { get; set; } = "";

    public string Password { get; set; } = "";

    public string NombreCompleto { get; set; } = "";
}

/// <summary>
/// Datos semilla (MVP §3.3). Las contraseñas van en los secretos de usuario, nunca en el repositorio (RNF-25):
/// <c>dotnet user-secrets set "Semilla:Operador:Password" "…"</c>.
/// </summary>
public sealed class OpcionesSemilla
{
    public const string Seccion = "Semilla";

    public CuentaSemilla? Administrador { get; set; }

    public CuentaSemilla? Operador { get; set; }

    /// <summary>Quien apaga la alarma en la demo: el Operador ya no puede (RF-06 CA3).</summary>
    public CuentaSemilla? Seguridad { get; set; }
}

public static class InicializacionBaseDeDatos
{
    /// <summary>
    /// Al arrancar: aplica las migraciones pendientes, crea los roles y las cuentas del personal que falten y,
    /// si la base no tiene clientes, carga los de prueba. Se puede correr en cada arranque sin duplicar nada.
    /// </summary>
    public static async Task InicializarBaseDeDatosAsync(this WebApplication app)
    {
        using var alcance = app.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var logger = servicios.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializacionBaseDeDatos));
        var db = servicios.GetRequiredService<TransitGateDbContext>();

        await db.Database.MigrateAsync();

        var roles = servicios.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var rol in Roles.Todos)
        {
            if (!await roles.RoleExistsAsync(rol))
                await roles.CreateAsync(new IdentityRole<Guid>(rol));
        }

        var semilla = app.Configuration.GetSection(OpcionesSemilla.Seccion).Get<OpcionesSemilla>() ?? new();
        var usuarios = servicios.GetRequiredService<UserManager<Usuario>>();
        await CrearPersonalAsync(usuarios, semilla.Administrador, Roles.Administrador, logger);
        await CrearPersonalAsync(usuarios, semilla.Operador, Roles.Operador, logger);
        await CrearPersonalAsync(usuarios, semilla.Seguridad, Roles.Seguridad, logger);

        if (!await db.Clientes.AnyAsync())
        {
            var datos = app.Configuration.GetSection(DatosDePrueba.Seccion).Get<DatosDePrueba>() ?? new();
            List<Cliente> clientes = [];
            List<Tarjeta> tarjetas = [];
            List<SoporteTarjeta> soportes = [];
            SemillaCuentas.Cargar(datos, DateTime.UtcNow, clientes, tarjetas, soportes);
            db.Clientes.AddRange(clientes);
            db.Tarjetas.AddRange(tarjetas);
            db.Soportes.AddRange(soportes);
            await db.SaveChangesAsync();
            logger.LogInformation("Datos de prueba cargados: {Clientes} clientes", clientes.Count);
        }
    }

    private static async Task CrearPersonalAsync(UserManager<Usuario> usuarios, CuentaSemilla? cuenta, string rol, ILogger logger)
    {
        if (cuenta is null || string.IsNullOrWhiteSpace(cuenta.Correo) || string.IsNullOrWhiteSpace(cuenta.Password))
        {
            logger.LogWarning(
                "Sin cuenta semilla de {Rol}: configura Semilla:{Rol}:Correo y Semilla:{Rol}:Password en los secretos de usuario", rol, rol, rol);
            return;
        }

        var correo = cuenta.Correo.Trim().ToLowerInvariant();
        if (await usuarios.FindByEmailAsync(correo) is not null)
            return;

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = correo,
            Email = correo,
            NombreCompleto = string.IsNullOrWhiteSpace(cuenta.NombreCompleto) ? rol : cuenta.NombreCompleto,
        };
        var creado = await usuarios.CreateAsync(usuario, cuenta.Password);
        if (!creado.Succeeded)
        {
            logger.LogError("No se pudo crear la cuenta semilla de {Rol}: {Errores}", rol, string.Join("; ", creado.Errors.Select(e => e.Description)));
            return;
        }

        await usuarios.AddToRoleAsync(usuario, rol);
        logger.LogInformation("Cuenta semilla de {Rol} creada", rol);
    }
}
