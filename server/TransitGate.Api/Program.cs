using Scalar.AspNetCore;
using TransitGate.Api;
using TransitGate.Api.Diagnostico;
using TransitGate.Api.Endpoints;
using TransitGate.Api.Esqueleto;
using TransitGate.Api.Seguridad;
using TransitGate.Api.TiempoReal;
using TransitGate.Application.Accesos;
using TransitGate.Application.Alertas;
using TransitGate.Application.Cuentas;
using TransitGate.Application.Dispositivos;
using TransitGate.Application.Notificaciones;
using TransitGate.Application.Taquilla;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;

var builder = WebApplication.CreateBuilder(args);

// Una línea por evento, con hora UTC al milisegundo: es la evidencia de las mediciones (RNF-04, RNF-06).
builder.Logging.AddSimpleConsole(opciones =>
{
    opciones.SingleLine = true;
    opciones.UseUtcTimestamp = true;
    opciones.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

// Los números del JSON van como números (Contrato API): sin esto .NET también acepta "12" y
// el OpenAPI los publica como number | string, que llega así a los tipos de la web.
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.Configure<OpcionesOperacion>(builder.Configuration.GetSection(OpcionesOperacion.Seccion));
builder.Services.AddSingleton<IParametrosOperacion, ParametrosDesdeConfiguracion>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CerrojoTorniquetes>();
builder.Services.AddSingleton<BitacoraTorniquete>();
builder.Services.AddScoped<ValidarLectura>();
builder.Services.AddScoped<ProcesarLatido>();
builder.Services.AddScoped<ConsultarTorniquetes>();
builder.Services.AddScoped<ConsultarIntentos>();
builder.Services.AddScoped<RegistrarCruce>();
builder.Services.AddScoped<ApagarAlarma>();
builder.Services.AddScoped<ConsultarAlertas>();
builder.Services.AddScoped<ActivarModoRegistro>();
builder.Services.AddScoped<ConsultarSolicitudes>();
builder.Services.AddSingleton<RespuestasDeRegistro>();
builder.Services.Configure<OpcionesCuentas>(builder.Configuration.GetSection(OpcionesCuentas.Seccion));
builder.Services.AddSingleton<IParametrosCuentas, ParametrosCuentasDesdeConfiguracion>();
builder.Services.AddSingleton<CerrojoTarjetas>();
builder.Services.AddScoped<ServicioCuentas>();
builder.Services.AddScoped<ISoporteResolver>(sp => sp.GetRequiredService<ServicioCuentas>());
builder.Services.AddScoped<ISaldoService>(sp => sp.GetRequiredService<ServicioCuentas>());
builder.Services.AddScoped<ISolicitudesTarjeta>(sp => sp.GetRequiredService<ServicioCuentas>());
builder.Services.Configure<OpcionesDispositivos>(builder.Configuration.GetSection(OpcionesDispositivos.Seccion));
builder.Services.AddSingleton<IClavesDispositivos, ClavesDesdeConfiguracion>();
builder.Services.AddSingleton<VerificadorClaveDispositivo>();
builder.Services.AddSingleton<INotificador, NotificadorSignalR>();
builder.Services.AddSingleton<MonitorTorniquetes>();
builder.Services.AddHostedService<ServicioMonitorTorniquetes>();
builder.Services.AddEsqueletoEnMemoria(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<MiddlewareTiempos>();

// La web (React) compilada en wwwroot, en el mismo puerto que la API: sin CORS (Contratos de integración, C6).
app.UseStaticFiles();

app.MapOpenApi();               // /openapi/v1.json
app.MapScalarApiReference();    // /scalar

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow }))
    .WithTags("Diagnóstico");

app.MapDispositivos();
app.MapTaquilla();
app.MapHub<EventosHub>(EventosHub.Ruta);

// Cualquier otra ruta es de la web (React Router resuelve la página en el navegador),
// menos las de la API, que siguen respondiendo 404 en lugar de devolver index.html.
app.MapFallback("/api/{**resto}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
