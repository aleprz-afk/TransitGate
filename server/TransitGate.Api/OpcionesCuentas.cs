using Microsoft.Extensions.Options;
using TransitGate.Application.Cuentas;

namespace TransitGate.Api;

/// <summary>Tarifa y límites de recarga (sección Cuentas de appsettings.json, RNF-40).</summary>
public sealed class OpcionesCuentas
{
    public const string Seccion = "Cuentas";

    /// <summary>Tarifa fija del MVP hasta RF-41.</summary>
    public decimal Tarifa { get; set; } = 3200;

    /// <summary>S8.</summary>
    public decimal MontoMinRecarga { get; set; } = 1000;

    /// <summary>S8.</summary>
    public decimal MontoMaxRecarga { get; set; } = 200000;
}

public sealed class ParametrosCuentasDesdeConfiguracion(IOptionsMonitor<OpcionesCuentas> opciones) : IParametrosCuentas
{
    public decimal Tarifa => opciones.CurrentValue.Tarifa;

    public decimal MontoMinRecarga => opciones.CurrentValue.MontoMinRecarga;

    public decimal MontoMaxRecarga => opciones.CurrentValue.MontoMaxRecarga;
}
