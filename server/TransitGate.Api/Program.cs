using Scalar.AspNetCore;
using TransitGate.Api;
using TransitGate.Api.Diagnostico;
using TransitGate.Api.Endpoints;
using TransitGate.Api.Esqueleto;
using TransitGate.Api.Seguridad;
using TransitGate.Api.TiempoReal;
using TransitGate.Application.Accesos;
using TransitGate.Application.Dispositivos;
using TransitGate.Application.Notificaciones;
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
builder.Services.Configure<OpcionesDispositivos>(builder.Configuration.GetSection(OpcionesDispositivos.Seccion));
builder.Services.AddSingleton<IClavesDispositivos, ClavesDesdeConfiguracion>();
builder.Services.AddSingleton<VerificadorClaveDispositivo>();
builder.Services.AddSingleton<INotificador, NotificadorSignalR>();
builder.Services.AddSingleton<MonitorTorniquetes>();
builder.Services.AddHostedService<ServicioMonitorTorniquetes>();
builder.Services.AddEsqueletoEnMemoria(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<MiddlewareTiempos>();

app.MapOpenApi();               // /openapi/v1.json
app.MapScalarApiReference();    // /scalar

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow }))
    .WithTags("Diagnóstico");

app.MapDispositivos();
app.MapHub<EventosHub>(EventosHub.Ruta);

app.Run();
