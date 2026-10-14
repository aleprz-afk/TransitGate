using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;

namespace TransitGate.Api.Esqueleto;

public static class RegistroEsqueleto
{
    /// <summary>
    /// Implementaciones en memoria de los puertos de Application. Cuando Infrastructure tenga EF Core,
    /// se cambia esta llamada por la que registra la base de datos.
    /// </summary>
    public static IServiceCollection AddEsqueletoEnMemoria(this IServiceCollection services, IConfiguration configuracion)
    {
        services.Configure<DatosDePrueba>(configuracion.GetSection(DatosDePrueba.Seccion));
        services.AddSingleton<ITorniqueteRepositorio, TorniqueteRepositorioEnMemoria>();
        services.AddSingleton<ILecturasProcesadas, LecturasProcesadasEnMemoria>();
        services.AddSingleton<CuentasDePrueba>();
        services.AddSingleton<ISoporteResolver>(sp => sp.GetRequiredService<CuentasDePrueba>());
        services.AddSingleton<ISaldoService>(sp => sp.GetRequiredService<CuentasDePrueba>());
        services.AddSingleton<EstacionEnMemoria>();
        return services;
    }
}
