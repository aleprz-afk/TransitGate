using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TransitGate.Api;
using TransitGate.Api.Endpoints;
using TransitGate.Api.Esqueleto;
using TransitGate.Application.Torniquetes;
using TransitGate.Application.Validacion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<OpcionesOperacion>(builder.Configuration.GetSection(OpcionesOperacion.Seccion));
builder.Services.AddTransient<IParametrosOperacion>(sp =>
    sp.GetRequiredService<IOptionsMonitor<OpcionesOperacion>>().CurrentValue);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CerrojoTorniquetes>();
builder.Services.AddScoped<ValidarLectura>();
builder.Services.AddEsqueletoEnMemoria(builder.Configuration);

var app = builder.Build();

app.MapOpenApi();               // /openapi/v1.json
app.MapScalarApiReference();    // /scalar

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow }))
    .WithTags("Diagnóstico");

app.MapDispositivos();

app.Run();
