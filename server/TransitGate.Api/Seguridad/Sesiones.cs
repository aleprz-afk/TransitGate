using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TransitGate.Api.Contratos;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Api.Seguridad;

/// <summary>Quién puede usar cada grupo de rutas (RF-06, RNF-19).</summary>
public static class Politicas
{
    public const string Cliente = "Cliente";

    /// <summary>En el MVP hay un solo rol de personal que hace taquilla y seguridad (MVP §3.1).</summary>
    public const string Personal = "Personal";
}

public static class Sesiones
{
    /// <summary>
    /// Sesión con la cookie de Identity (Contrato §2): vence a los 60 min sin uso (RNF-18). Sin sesión la API
    /// responde 401 y sin permiso 403, nunca una redirección: la web decide a dónde llevar al usuario (RNF-19).
    /// </summary>
    public static IServiceCollection AddSesiones(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(opciones =>
        {
            opciones.Cookie.Name = "transitgate";
            opciones.Cookie.HttpOnly = true;
            opciones.Cookie.SameSite = SameSiteMode.Lax;
            opciones.ExpireTimeSpan = TimeSpan.FromMinutes(60);
            opciones.SlidingExpiration = true;
            opciones.Events.OnRedirectToLogin = contexto => Responder(contexto.Response, 401, "SesionRequerida", "Inicia sesión para continuar.");
            opciones.Events.OnRedirectToAccessDenied = contexto => Responder(contexto.Response, 403, "SinPermiso", "Tu cuenta no tiene permiso para esto.");
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Cliente, p => p.RequireRole(Roles.Cliente))
            .AddPolicy(Politicas.Personal, p => p.RequireRole(Roles.Operador, Roles.Seguridad, Roles.Administrador));

        return services;
    }

    /// <summary>El Id del usuario de la sesión; en un cliente es el Id del Cliente.</summary>
    public static Guid IdUsuario(this ClaimsPrincipal usuario) =>
        Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static Task Responder(HttpResponse respuesta, int estado, string error, string mensaje)
    {
        respuesta.StatusCode = estado;
        return respuesta.WriteAsJsonAsync(new ErrorApi(error, mensaje));
    }
}
