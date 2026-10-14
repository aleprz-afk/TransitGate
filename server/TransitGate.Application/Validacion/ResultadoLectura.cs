using TransitGate.Domain.Accesos;

namespace TransitGate.Application.Validacion;

public sealed record ResultadoLectura(bool Autorizado, long? PasoId, decimal? SaldoRestante, MotivoIntento? Motivo)
{
    public static ResultadoLectura Abrir(long pasoId, decimal saldoRestante) => new(true, pasoId, saldoRestante, null);

    public static ResultadoLectura Denegar(MotivoIntento motivo) => new(false, null, null, motivo);

    public static ResultadoLectura Desde(IntentoAcceso intento) =>
        intento.Resultado == ResultadoIntento.Exitoso
            ? Abrir(intento.PasoId!.Value, intento.SaldoRestante!.Value)
            : Denegar(intento.Motivo!.Value);
}
