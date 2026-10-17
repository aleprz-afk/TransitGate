using TransitGate.Application.Cuentas;
using TransitGate.Domain.Accesos;

namespace TransitGate.Application.Validacion;

/// <summary>Qué hace el ESP32 con la respuesta (Contrato §6.1 y §4): abrir, no abrir, o nada porque se registró la tarjeta.</summary>
public enum AccionLectura
{
    Abrir,
    Denegar,

    /// <summary>En modo registro: el UID se ligó a la solicitud y el brazo no se mueve (RF-10 CA1).</summary>
    Registrada,
}

/// <summary>
/// <paramref name="Motivo"/> es un MotivoIntento en un paso rechazado, o un ResultadoLigar (UidYaRegistrado,
/// SolicitudNoPendiente) en modo registro, que no deja IntentoAcceso (RF-23 CA3).
/// </summary>
public sealed record ResultadoLectura(AccionLectura Accion, long? PasoId, decimal? SaldoRestante, string? Motivo)
{
    public bool Autorizado => Accion == AccionLectura.Abrir;

    public static ResultadoLectura Abrir(long pasoId, decimal saldoRestante) => new(AccionLectura.Abrir, pasoId, saldoRestante, null);

    public static ResultadoLectura Denegar(MotivoIntento motivo) => new(AccionLectura.Denegar, null, null, motivo.ToString());

    public static ResultadoLectura Registrada() => new(AccionLectura.Registrada, null, null, null);

    public static ResultadoLectura RegistroDenegado(ResultadoLigar motivo) => new(AccionLectura.Denegar, null, null, motivo.ToString());

    public static ResultadoLectura Desde(IntentoAcceso intento) =>
        intento.Resultado == ResultadoIntento.Exitoso
            ? Abrir(intento.PasoId!.Value, intento.SaldoRestante!.Value)
            : Denegar(intento.Motivo!.Value);
}
