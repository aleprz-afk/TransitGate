namespace TransitGate.Application.Cuentas;

/// <summary>
/// La cuenta con la que el cliente inicia sesión (correo y contraseña). La implementa Identity en
/// Infrastructure; la contraseña nunca llega al dominio y se guarda con hash y sal (RNF-17).
/// </summary>
public interface ICuentasAcceso
{
    /// <summary>Devuelve null si se creó, o el error (por ejemplo, una contraseña que Identity no acepta).</summary>
    Task<ErrorCuenta?> CrearClienteAsync(Guid clienteId, string correo, string password, CancellationToken ct = default);
}
