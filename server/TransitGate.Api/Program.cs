using Scalar.AspNetCore;
using TransitGate.Api;
using TransitGate.Api.Endpoints;
using TransitGate.Api.Esqueleto;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<OpcionesOperacion>(builder.Configuration.GetSection(OpcionesOperacion.Seccion));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EstacionEnMemoria>();

var app = builder.Build();

app.MapOpenApi();               // /openapi/v1.json
app.MapScalarApiReference();    // /scalar

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow }))
    .WithTags("Diagnóstico");

app.MapDispositivos();

app.Run();
