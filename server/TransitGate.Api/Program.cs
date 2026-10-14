using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TransitGate.Api;
using TransitGate.Api.Endpoints;
using TransitGate.Api.Esqueleto;
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
builder.Services.Configure<OpcionesOperacion>(builder.Configuration.GetSection(OpcionesOperacion.Seccion));
builder.Services.AddTransient<IParametrosOperacion>(sp =>
    sp.GetRequiredService<IOptionsMonitor<OpcionesOperacion>>().CurrentValue);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CerrojoTorniquetes>();
builder.Services.AddSingleton<BitacoraTorniquete>();
builder.Services.AddScoped<ValidarLectura>();
builder.Services.AddScoped<ProcesarLatido>();
builder.Services.AddScoped<ConsultarTorniquetes>();
builder.Services.AddEsqueletoEnMemoria(builder.Configuration);

var app = builder.Build();

app.MapOpenApi();               // /openapi/v1.json
app.MapScalarApiReference();    // /scalar

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow }))
    .WithTags("Diagnóstico");

app.MapDispositivos();

app.Run();
