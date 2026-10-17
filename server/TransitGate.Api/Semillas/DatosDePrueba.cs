using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;

namespace TransitGate.Api.Semillas;

/// <summary>Clientes de prueba (sección DatosDePrueba de appsettings.json); se cargan en memoria al arrancar.</summary>
public sealed class DatosDePrueba
{
    public const string Seccion = "DatosDePrueba";

    /// <summary>Clientes con su tarjeta física ya entregada, en el estado indicado.</summary>
    public List<TarjetaDePrueba> Tarjetas { get; set; } = [];

    /// <summary>Clientes que pidieron la tarjeta física (RF-08) y esperan recogerla en taquilla.</summary>
    public List<SolicitudDePrueba> Solicitudes { get; set; } = [];
}

public sealed class TarjetaDePrueba
{
    public string Uid { get; set; } = "";

    public EstadoSoporte Estado { get; set; } = EstadoSoporte.Activa;

    public decimal Saldo { get; set; }
}

public sealed class SolicitudDePrueba
{
    public string NombreCompleto { get; set; } = "";

    public string Documento { get; set; } = "";

    public decimal Saldo { get; set; }
}

public static class SemillaCuentas
{
    /// <summary>Crea un Cliente con su Tarjeta y su soporte físico por cada tarjeta y solicitud de prueba.</summary>
    public static void Cargar(
        DatosDePrueba datos, DateTime ahora,
        IList<Cliente> clientes, IList<Tarjeta> tarjetas, IList<SoporteTarjeta> soportes)
    {
        // Fechas escalonadas, para que la lista de la taquilla tenga un orden estable (RF-36 CA1).
        var fecha = ahora.AddHours(-1);
        var numero = 0;

        foreach (var prueba in datos.Tarjetas)
        {
            var (tarjeta, soporte) = Crear($"Cliente de prueba {prueba.Uid}", $"100000000{++numero}", prueba.Saldo, fecha = fecha.AddMinutes(1));
            soporte.Entregar(prueba.Uid.ToUpperInvariant(), fecha);
            if (prueba.Estado is EstadoSoporte.Activa or EstadoSoporte.Bloqueada)
                soporte.Activar(fecha);
            if (prueba.Estado == EstadoSoporte.Bloqueada)
                soporte.Bloquear(MotivoBloqueo.Reporte, fecha);
        }

        foreach (var prueba in datos.Solicitudes)
            Crear(prueba.NombreCompleto, prueba.Documento, prueba.Saldo, fecha = fecha.AddMinutes(1));

        (Tarjeta, SoporteTarjeta) Crear(string nombre, string documento, decimal saldo, DateTime cuando)
        {
            var cliente = new Cliente(
                Guid.NewGuid(), nombre, "CC", documento, $"prueba{documento}@transitgate.test", "3000000000", cuando);
            var tarjeta = new Tarjeta(Guid.NewGuid(), cliente.Id);
            if (saldo > 0)
                tarjeta.Acreditar(saldo);
            var soporte = SoporteTarjeta.SolicitarFisico(Guid.NewGuid(), tarjeta.Id, cuando);

            clientes.Add(cliente);
            tarjetas.Add(tarjeta);
            soportes.Add(soporte);
            return (tarjeta, soporte);
        }
    }
}
