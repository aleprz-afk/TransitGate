using Microsoft.Extensions.Logging.Abstractions;
using TransitGate.Application.Dispositivos;

namespace TransitGate.Application.Tests.Dispositivos;

public class VerificadorClaveDispositivoTests
{
    private readonly VerificadorClaveDispositivo _verificador = new(
        new ClavesFijas(), NullLogger<VerificadorClaveDispositivo>.Instance);

    [Fact] // RNF-20
    public void La_clave_del_dispositivo_correcto_es_valida()
    {
        Assert.Equal(ResultadoClave.Valida, _verificador.Verificar("tq_1", TipoDispositivo.Esp32, "clave-esp32"));
        Assert.Equal(ResultadoClave.Valida, _verificador.Verificar("tq_1", TipoDispositivo.Vision, "clave-vision"));
    }

    [Theory] // RNF-20
    [InlineData(null, ResultadoClave.Ausente)]
    [InlineData("", ResultadoClave.Ausente)]
    [InlineData("otra", ResultadoClave.Invalida)]
    [InlineData("clave-vision", ResultadoClave.Invalida)]  // la de la visión no sirve para el ESP32
    public void Sin_clave_o_con_otra_clave_no_es_valida(string? presentada, ResultadoClave esperado)
    {
        Assert.Equal(esperado, _verificador.Verificar("tq_1", TipoDispositivo.Esp32, presentada));
    }

    [Fact] // HU-28 CA2: la credencial de un torniquete no sirve en otro
    public void Un_torniquete_sin_clave_configurada_no_acepta_ninguna()
    {
        Assert.Equal(ResultadoClave.SinConfigurar, _verificador.Verificar("tq_2", TipoDispositivo.Esp32, "clave-esp32"));
    }

    private sealed class ClavesFijas : IClavesDispositivos
    {
        public string? ClaveDe(string torniqueteId, TipoDispositivo dispositivo) => (torniqueteId, dispositivo) switch
        {
            ("tq_1", TipoDispositivo.Esp32) => "clave-esp32",
            ("tq_1", TipoDispositivo.Vision) => "clave-vision",
            _ => null,
        };
    }
}
