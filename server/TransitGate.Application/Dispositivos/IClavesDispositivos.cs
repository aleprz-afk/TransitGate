namespace TransitGate.Application.Dispositivos;

/// <summary>Cada torniquete tiene una credencial para su ESP32 y otra para su servicio de visión (Contrato §2).</summary>
public enum TipoDispositivo
{
    Esp32,
    Vision,
}

/// <summary>
/// De dónde salen las credenciales de los dispositivos. Hoy de los secretos de usuario (RNF-25);
/// con RF-43 se generan al registrar el torniquete y se guardan en la base de datos.
/// </summary>
public interface IClavesDispositivos
{
    /// <summary>Null si ese torniquete no tiene clave configurada para ese dispositivo.</summary>
    string? ClaveDe(string torniqueteId, TipoDispositivo dispositivo);
}
