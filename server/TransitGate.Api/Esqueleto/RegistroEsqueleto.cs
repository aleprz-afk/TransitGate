using TransitGate.Application;
using TransitGate.Application.Accesos;
using TransitGate.Application.Alertas;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Torniquetes;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Api.Esqueleto;

public static class RegistroEsqueleto
{
    /// <summary>
    /// Implementaciones en memoria de los puertos de Application, con los datos de prueba ya cargados. Cuando
    /// Infrastructure tenga EF Core, se cambia esta llamada por la que registra la base de datos.
    /// </summary>
    public static IServiceCollection AddEsqueletoEnMemoria(this IServiceCollection services, IConfiguration configuracion)
    {
        services.AddSingleton<ITorniqueteRepositorio, TorniqueteRepositorioEnMemoria>();
        services.AddSingleton<IIntentoRepositorio, IntentoRepositorioEnMemoria>();
        services.AddSingleton<IUnidadDeTrabajo, UnidadDeTrabajoEnMemoria>();
        services.AddSingleton<ICruceRepositorio, CruceRepositorioEnMemoria>();
        services.AddSingleton<IAlertaRepositorio, AlertaRepositorioEnMemoria>();

        var clientes = new ClienteRepositorioEnMemoria();
        var tarjetas = new TarjetaRepositorioEnMemoria();
        var soportes = new SoporteRepositorioEnMemoria();
        CargarDatosDePrueba(configuracion.GetSection(DatosDePrueba.Seccion).Get<DatosDePrueba>() ?? new(), clientes, tarjetas, soportes);

        services.AddSingleton<IClienteRepositorio>(clientes);
        services.AddSingleton<ITarjetaRepositorio>(tarjetas);
        services.AddSingleton<ISoporteRepositorio>(soportes);
        services.AddSingleton<IRecargaRepositorio, RecargaRepositorioEnMemoria>();
        services.AddSingleton<IDebitoRepositorio, DebitoRepositorioEnMemoria>();
        return services;
    }

    private static void CargarDatosDePrueba(
        DatosDePrueba datos, IClienteRepositorio clientes, ITarjetaRepositorio tarjetas, ISoporteRepositorio soportes)
    {
        List<Cliente> nuevosClientes = [];
        List<Tarjeta> nuevasTarjetas = [];
        List<SoporteTarjeta> nuevosSoportes = [];
        SemillaCuentas.Cargar(datos, DateTime.UtcNow, nuevosClientes, nuevasTarjetas, nuevosSoportes);

        // En memoria, AgregarAsync termina al instante.
        nuevosClientes.ForEach(c => clientes.AgregarAsync(c).GetAwaiter().GetResult());
        nuevasTarjetas.ForEach(t => tarjetas.AgregarAsync(t).GetAwaiter().GetResult());
        nuevosSoportes.ForEach(s => soportes.AgregarAsync(s).GetAwaiter().GetResult());
    }
}
