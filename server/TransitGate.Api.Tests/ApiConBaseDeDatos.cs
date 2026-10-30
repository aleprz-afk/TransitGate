using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace TransitGate.Api.Tests;

/// <summary>
/// La API completa contra un PostgreSQL de verdad, en un contenedor que se crea para las pruebas y se borra al
/// terminar (necesita Docker Desktop abierto). Todas las pruebas de la colección comparten la base: cada una
/// usa sus propios correos y documentos.
/// </summary>
public sealed class ApiConBaseDeDatos : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string CorreoOperador = "operador@prueba.test";
    public const string PasswordOperador = "operador-prueba";
    public const string CorreoSeguridad = "seguridad@prueba.test";
    public const string PasswordSeguridad = "seguridad-prueba";
    public const string CorreoAdministrador = "admin@prueba.test";
    public const string PasswordAdministrador = "admin-prueba";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public string CadenaConexion => _postgres.GetConnectionString();

    public Task InitializeAsync() => _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Fuera de Development no se leen los secretos de usuario de la laptop.
        builder.UseEnvironment("Pruebas");
        builder.UseSetting("ConnectionStrings:TransitGate", CadenaConexion);
        builder.UseSetting("Semilla:Operador:Correo", CorreoOperador);
        builder.UseSetting("Semilla:Operador:Password", PasswordOperador);
        builder.UseSetting("Semilla:Seguridad:Correo", CorreoSeguridad);
        builder.UseSetting("Semilla:Seguridad:Password", PasswordSeguridad);
        builder.UseSetting("Semilla:Administrador:Correo", CorreoAdministrador);
        builder.UseSetting("Semilla:Administrador:Password", PasswordAdministrador);
    }

    /// <summary>Un navegador con su propia cookie de sesión.</summary>
    public HttpClient Navegador() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    public Task<HttpClient> OperadorAsync() => PersonalAsync(CorreoOperador, PasswordOperador);

    public Task<HttpClient> SeguridadAsync() => PersonalAsync(CorreoSeguridad, PasswordSeguridad);

    public Task<HttpClient> AdministradorAsync() => PersonalAsync(CorreoAdministrador, PasswordAdministrador);

    private async Task<HttpClient> PersonalAsync(string correo, string password)
    {
        var navegador = Navegador();
        var respuesta = await navegador.PostAsJsonAsync("/api/auth/login", new { correo, password });
        respuesta.EnsureSuccessStatusCode();
        return navegador;
    }

    /// <summary>Registra un cliente nuevo y devuelve su navegador, ya con la sesión iniciada (RF-01 CA1).</summary>
    public async Task<HttpClient> ClienteAsync(string documento)
    {
        var navegador = Navegador();
        var respuesta = await navegador.PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = "Cliente de integración",
            tipoDocumento = "CC",
            numeroDocumento = documento,
            correo = $"c{documento}@prueba.test",
            telefono = "3001234567",
            password = "clave-segura",
            autorizaTratamientoDatos = true,
        });
        respuesta.EnsureSuccessStatusCode();
        return navegador;
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<ApiConBaseDeDatos>
{
    public const string Nombre = "API con PostgreSQL";
}
