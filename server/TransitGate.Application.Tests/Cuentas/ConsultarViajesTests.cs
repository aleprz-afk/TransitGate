using TransitGate.Application.Cuentas;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Application.Tests.Cuentas;

public class ConsultarViajesTests
{
    private static readonly DateTime Ahora = ModuloCuentasFalso.Ahora;

    private readonly ModuloCuentasFalso _cuentas = new();
    private readonly IntentosFalsos _intentos = new();
    private readonly ConsultarViajes _consultar;

    public ConsultarViajesTests()
    {
        _consultar = new ConsultarViajes(_cuentas.Tarjetas, _intentos, new TorniquetesFalsos(new Torniquete("tq_1", "Torniquete 1")));
    }

    private IntentoAcceso Exitoso(long id, Guid tarjetaId, decimal saldo) => IntentoAcceso.Exitoso(
        id, "tq_1", $"a1f3-{id:0000}", Ahora.AddMinutes(id), TipoSoporte.Fisico, "04A1B2C3", tarjetaId, 3200, id, saldo);

    [Fact] // RF-39 CA1 y CA2
    public async Task Muestra_los_pasos_exitosos_y_rechazados_con_torniquete_tarifa_y_saldo_el_mas_reciente_primero()
    {
        var (cliente, tarjeta, _) = _cuentas.Cliente();
        _intentos.Guardados.Add(Exitoso(1, tarjeta.Id, 3600));
        _intentos.Guardados.Add(Exitoso(2, tarjeta.Id, 400));
        _intentos.Guardados.Add(IntentoAcceso.Rechazado(
            3, "tq_1", "a1f3-0003", Ahora.AddMinutes(3), TipoSoporte.Fisico, "04A1B2C3", tarjeta.Id, MotivoIntento.SaldoInsuficiente));
        _intentos.Guardados.Add(Exitoso(4, Guid.NewGuid(), 0));   // de otra tarjeta

        var viajes = (await _consultar.EjecutarAsync(cliente.Id)).Valor!;

        Assert.Equal([3L, 2L, 1L], viajes.Select(v => v.Intento.Id));
        Assert.All(viajes, v => Assert.Equal("Torniquete 1", v.TorniqueteNombre));
        Assert.Equal(MotivoIntento.SaldoInsuficiente, viajes[0].Intento.Motivo);
        Assert.Equal((3200m, 400m), (viajes[1].Intento.TarifaCobrada!.Value, viajes[1].Intento.SaldoRestante!.Value));
    }

    [Fact] // RF-39 CA3
    public async Task Los_viajes_siguen_despues_de_cambiar_de_soporte_porque_van_por_tarjeta()
    {
        var (cliente, tarjeta, soporte) = _cuentas.Cliente(estadoFisico: EstadoSoporte.Activa);
        _intentos.Guardados.Add(Exitoso(1, tarjeta.Id, 3600));
        soporte!.Bloquear(MotivoBloqueo.Perdida, Ahora);

        Assert.Single((await _consultar.EjecutarAsync(cliente.Id)).Valor!);
    }
}
