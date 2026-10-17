using TransitGate.Application.Cuentas;

namespace TransitGate.Application.Tests.Cuentas;

public class RegistrarClienteTests
{
    private readonly ModuloCuentasFalso _cuentas = new();
    private readonly RegistrarCliente _registrar;

    public RegistrarClienteTests()
    {
        _registrar = new RegistrarCliente(
            _cuentas.Clientes, _cuentas.Tarjetas, _cuentas.CuentasAcceso, _cuentas.UnidadDeTrabajo, _cuentas.Reloj);
    }

    private static DatosRegistro Datos(
        string nombre = "Ana Ríos", string tipo = "CC", string documento = "1032456789",
        string correo = "ana@example.com", string telefono = "3001234567", string password = "segura123",
        bool autoriza = true) =>
        new(nombre, tipo, documento, correo, telefono, password, autoriza);

    [Fact] // RF-01 CA1, RF-07 CA1
    public async Task Con_datos_validos_crea_el_cliente_su_tarjeta_en_cero_y_su_cuenta_de_acceso()
    {
        var resultado = await _registrar.EjecutarAsync(Datos(correo: "  Ana@Example.com "));

        Assert.True(resultado.Exito);
        var cliente = Assert.Single(_cuentas.Clientes.Guardados);
        var tarjeta = Assert.Single(_cuentas.Tarjetas.Guardadas);
        Assert.Equal(resultado.Valor, new ClienteRegistrado(cliente.Id, tarjeta.Id));
        Assert.Equal("ana@example.com", cliente.Correo);
        Assert.Equal(cliente.Id, tarjeta.ClienteId);
        Assert.Equal(0, tarjeta.Saldo);
        Assert.Empty(_cuentas.Soportes.Guardados);
        Assert.Equal((cliente.Id, "ana@example.com"), Assert.Single(_cuentas.CuentasAcceso.Creadas));
        Assert.Equal(1, _cuentas.UnidadDeTrabajo.VecesGuardada);
    }

    [Theory] // RF-01 CA2
    [InlineData("ANA@example.com", "999999999", "CorreoDuplicado")]
    [InlineData("otra@example.com", "1032456789", "DocumentoDuplicado")]
    public async Task Un_correo_o_documento_repetido_no_crea_la_cuenta(string correo, string documento, string error)
    {
        await _registrar.EjecutarAsync(Datos());

        var resultado = await _registrar.EjecutarAsync(Datos(correo: correo, documento: documento));

        Assert.Equal(error, resultado.Error!.Codigo);
        Assert.Single(_cuentas.Clientes.Guardados);
    }

    [Theory] // RF-01 CA3
    [InlineData("", "CC", "1032456789", "ana@example.com", "3001234567", "NombreRequerido")]
    [InlineData("Ana", "XX", "1032456789", "ana@example.com", "3001234567", "TipoDocumentoInvalido")]
    [InlineData("Ana", "CC", "10324A6789", "ana@example.com", "3001234567", "DocumentoInvalido")]
    [InlineData("Ana", "CC", "1032456789", "anaexample.com", "3001234567", "CorreoInvalido")]
    [InlineData("Ana", "CC", "1032456789", "ana@example.com", "300123", "TelefonoInvalido")]
    public async Task Un_campo_invalido_no_crea_la_cuenta_y_dice_cual(
        string nombre, string tipo, string documento, string correo, string telefono, string error)
    {
        var resultado = await _registrar.EjecutarAsync(Datos(nombre, tipo, documento, correo, telefono));

        Assert.Equal(error, resultado.Error!.Codigo);
        Assert.Equal(TipoError.Invalido, resultado.Error.Tipo);
        Assert.Empty(_cuentas.Clientes.Guardados);
        Assert.Empty(_cuentas.CuentasAcceso.Creadas);
    }

    [Fact]
    public async Task El_pasaporte_si_admite_letras()
    {
        var resultado = await _registrar.EjecutarAsync(Datos(tipo: "pp", documento: "AB123456"));

        Assert.True(resultado.Exito);
        Assert.Equal("PP", _cuentas.Clientes.Guardados[0].TipoDocumento);
    }

    [Fact] // RF-01 CA4
    public async Task Una_contrasena_de_menos_de_8_caracteres_no_crea_la_cuenta()
    {
        var resultado = await _registrar.EjecutarAsync(Datos(password: "1234567"));

        Assert.Equal("PasswordCorta", resultado.Error!.Codigo);
        Assert.Empty(_cuentas.Clientes.Guardados);
    }

    [Fact] // RF-01 CA5, RNF-28
    public async Task Sin_autorizar_el_tratamiento_de_datos_no_crea_la_cuenta()
    {
        var resultado = await _registrar.EjecutarAsync(Datos(autoriza: false));

        Assert.Equal("AutorizacionRequerida", resultado.Error!.Codigo);
        Assert.Empty(_cuentas.Clientes.Guardados);
    }

    [Fact]
    public async Task Si_la_cuenta_de_acceso_falla_no_queda_ni_el_cliente_ni_la_tarjeta()
    {
        _cuentas.CuentasAcceso.Error = ErrorCuenta.Invalido("PasswordInsegura", "La contraseña no es válida.");

        var resultado = await _registrar.EjecutarAsync(Datos());

        Assert.Equal("PasswordInsegura", resultado.Error!.Codigo);
        Assert.Empty(_cuentas.Clientes.Guardados);
        Assert.Empty(_cuentas.Tarjetas.Guardadas);
        Assert.Equal(0, _cuentas.UnidadDeTrabajo.VecesGuardada);
    }
}
