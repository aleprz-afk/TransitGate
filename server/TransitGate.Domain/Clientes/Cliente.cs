namespace TransitGate.Domain.Clientes;

/// <summary>
/// Persona registrada en el portal (RF-01). Solo los datos que se usan (RNF-27); la contraseña no vive aquí,
/// sino en la cuenta de acceso (Identity). Toda Tarjeta pertenece a un Cliente (arquitectura v2 §4).
/// </summary>
public class Cliente
{
    public Cliente(
        Guid id, string nombreCompleto, string tipoDocumento, string numeroDocumento,
        string correo, string telefono, DateTime fechaRegistro)
    {
        Id = id;
        NombreCompleto = nombreCompleto;
        TipoDocumento = tipoDocumento;
        NumeroDocumento = numeroDocumento;
        Correo = correo;
        Telefono = telefono;
        FechaRegistro = fechaRegistro;
        AutorizoTratamientoDatosEn = fechaRegistro;
    }

    public Guid Id { get; private set; }

    public string NombreCompleto { get; private set; }

    /// <summary>CC, CE, TI o PP.</summary>
    public string TipoDocumento { get; private set; }

    /// <summary>No se puede cambiar: identifica al cliente en taquilla (RF-05 CA3).</summary>
    public string NumeroDocumento { get; private set; }

    /// <summary>En minúsculas, para que la búsqueda de duplicados no dependa de mayúsculas (RF-01 CA2).</summary>
    public string Correo { get; private set; }

    public string Telefono { get; private set; }

    public DateTime FechaRegistro { get; private set; }

    /// <summary>Cuándo aceptó el tratamiento de datos; sin esa aceptación no hay registro (RNF-28).</summary>
    public DateTime AutorizoTratamientoDatosEn { get; private set; }

    public void CambiarTelefono(string telefono) => Telefono = telefono;
}
