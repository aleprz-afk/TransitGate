using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TransitGate.Application;
using TransitGate.Application.Accesos;
using TransitGate.Application.Alertas;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Infrastructure.Identidad;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Infrastructure;

public static class RegistroInfraestructura
{
    /// <summary>PostgreSQL, los repositorios y los almacenes de Identity.</summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, string cadenaConexion)
    {
        var baseDeDatos = NpgsqlDataSource.Create(cadenaConexion);
        services.AddSingleton(baseDeDatos);
        services.AddSingleton<Secuencias>();
        services.AddDbContext<TransitGateDbContext>(opciones => opciones.UseNpgsql(baseDeDatos));

        services.AddScoped<IUnidadDeTrabajo>(sp => sp.GetRequiredService<TransitGateDbContext>());
        services.AddSingleton<ITorniqueteRepositorio, TorniquetesEnMemoria>();
        services.AddScoped<IIntentoRepositorio, IntentoRepositorio>();
        services.AddScoped<ICruceRepositorio, CruceRepositorio>();
        services.AddScoped<IAlertaRepositorio, AlertaRepositorio>();
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<ITarjetaRepositorio, TarjetaRepositorio>();
        services.AddScoped<ISoporteRepositorio, SoporteRepositorio>();
        services.AddScoped<IRecargaRepositorio, RecargaRepositorio>();
        services.AddScoped<IDebitoRepositorio, DebitoRepositorio>();

        services.AddIdentityCore<Usuario>(opciones =>
            {
                // RF-01 CA4: al menos 8 caracteres, sin más reglas que confundan en la demo.
                opciones.Password.RequiredLength = RegistrarCliente.LargoMinimoPassword;
                opciones.Password.RequireDigit = false;
                opciones.Password.RequireLowercase = false;
                opciones.Password.RequireUppercase = false;
                opciones.Password.RequireNonAlphanumeric = false;
                opciones.User.RequireUniqueEmail = true;

                // RNF-22: 6 intentos fallidos bloquean el login por 5 minutos.
                opciones.Lockout.MaxFailedAccessAttempts = 6;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                opciones.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TransitGateDbContext>()
            .AddSignInManager();
        services.AddScoped<ICuentasAcceso, CuentasAccesoIdentity>();

        return services;
    }
}
