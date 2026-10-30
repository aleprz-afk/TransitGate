using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TransitGate.Api.Tests;

[Collection(ColeccionApi.Nombre)]
public class SesionYPermisosTests(ApiConBaseDeDatos api)
{
    [Theory] // RF-06 CA4, RNF-19: sin sesión, 401 (no una redirección)
    [InlineData("/api/tarjetas/mi-tarjeta")]
    [InlineData("/api/clientes/me")]
    [InlineData("/api/torniquetes")]
    [InlineData("/api/alertas")]
    [InlineData("/api/tarjetas/solicitudes")]
    public async Task Sin_sesion_responde_401(string ruta)
    {
        var respuesta = await api.Navegador().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("SesionRequerida", (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Theory] // RF-06 CA2
    [InlineData("/api/torniquetes")]
    [InlineData("/api/tarjetas/solicitudes")]
    public async Task Un_cliente_no_entra_al_panel(string ruta)
    {
        var cliente = await api.ClienteAsync("2000000001" + ruta.Length);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(ruta)).StatusCode);
    }

    [Fact] // RF-02 CA2
    public async Task El_personal_si_entra_al_panel_pero_no_a_mi_tarjeta()
    {
        var operador = await api.OperadorAsync();

        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync("/api/torniquetes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/tarjetas/mi-tarjeta")).StatusCode);
    }

    [Theory] // RF-06: los tres roles del personal ven el estado, los intentos y las alertas
    [InlineData("Operador")]
    [InlineData("Seguridad")]
    [InlineData("Administrador")]
    public async Task Todo_el_personal_ve_el_panel(string rol)
    {
        var personal = await Personal(rol);

        Assert.Equal(HttpStatusCode.OK, (await personal.GetAsync("/api/torniquetes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await personal.GetAsync("/api/torniquetes/tq_1/intentos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await personal.GetAsync("/api/alertas")).StatusCode);
    }

    [Fact] // RF-06 CA3, RF-34 CA2: el operador de taquilla no apaga la alarma
    public async Task El_operador_no_puede_apagar_la_alarma()
    {
        var operador = await api.OperadorAsync();

        var respuesta = await operador.PostAsync("/api/alertas/al_1/apagar-alarma", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("SinPermiso", (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Theory] // RF-06: Seguridad y Administrador sí pasan el permiso; la alerta no existe, así que 404 y no 403
    [InlineData("Seguridad")]
    [InlineData("Administrador")]
    public async Task Seguridad_y_administrador_pueden_apagar_la_alarma(string rol)
    {
        var personal = await Personal(rol);

        var respuesta = await personal.PostAsync("/api/alertas/al_999999/apagar-alarma", null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("AlertaNoEncontrada", (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact] // RF-06: Seguridad no entrega tarjetas en taquilla
    public async Task Seguridad_no_entra_a_la_taquilla()
    {
        var seguridad = await api.SeguridadAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await seguridad.GetAsync("/api/tarjetas/solicitudes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await seguridad.PostAsJsonAsync("/api/torniquetes/tq_1/modo-registro", new { solicitudId = "s_1" })).StatusCode);
    }

    [Theory] // RF-06: Operador y Administrador entregan tarjetas
    [InlineData("Operador")]
    [InlineData("Administrador")]
    public async Task Operador_y_administrador_entran_a_la_taquilla(string rol)
    {
        var personal = await Personal(rol);

        Assert.Equal(HttpStatusCode.OK, (await personal.GetAsync("/api/tarjetas/solicitudes")).StatusCode);
    }

    [Fact] // RF-02 CA1: el inicio de sesión dice el rol, para que la web arme el menú
    public async Task La_sesion_devuelve_el_rol_de_seguridad()
    {
        var seguridad = await api.SeguridadAsync();

        var sesion = await seguridad.GetFromJsonAsync<JsonElement>("/api/auth/sesion");

        Assert.Equal("Seguridad", sesion.GetProperty("rol").GetString());
    }

    [Fact] // RF-01 CA1, RF-06 CA1: cada cliente ve solo su tarjeta
    public async Task Cada_cliente_ve_su_propia_tarjeta()
    {
        var ana = await api.ClienteAsync("2000000002");
        var luis = await api.ClienteAsync("2000000003");
        await ana.PostAsJsonAsync("/api/tarjetas/mi-tarjeta/recargas", new { monto = 5000 });

        var deAna = await ana.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta");
        var deLuis = await luis.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta");

        Assert.Equal(5000, deAna.GetProperty("saldo").GetInt32());
        Assert.Equal(0, deLuis.GetProperty("saldo").GetInt32());
        Assert.NotEqual(deAna.GetProperty("tarjetaId").GetString(), deLuis.GetProperty("tarjetaId").GetString());
    }

    [Fact] // RF-02 CA3, RNF-22
    public async Task Seis_intentos_fallidos_bloquean_el_login()
    {
        await api.ClienteAsync("2000000004");
        var navegador = api.Navegador();
        var mala = new { correo = "c2000000004@prueba.test", password = "no-es-esta" };

        var errores = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var respuesta = await navegador.PostAsJsonAsync("/api/auth/login", mala);
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            errores.Add((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!);
        }
        var conLaBuena = await navegador.PostAsJsonAsync("/api/auth/login", new { correo = mala.correo, password = "clave-segura" });

        Assert.Equal([.. Enumerable.Repeat("CredencialesInvalidas", 5), "LoginBloqueado"], errores);
        Assert.Equal(HttpStatusCode.Unauthorized, conLaBuena.StatusCode);
    }

    [Fact] // RF-03 CA1
    public async Task Al_cerrar_sesion_las_paginas_protegidas_piden_sesion_otra_vez()
    {
        var cliente = await api.ClienteAsync("2000000005");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/sesion")).StatusCode);

        await cliente.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/tarjetas/mi-tarjeta")).StatusCode);
    }

    [Fact] // RF-01 CA2: el registro repetido no deja nada a medias
    public async Task Un_correo_repetido_responde_400_y_no_crea_otra_cuenta()
    {
        await api.ClienteAsync("2000000006");

        var respuesta = await api.Navegador().PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = "Otra persona", tipoDocumento = "CC", numeroDocumento = "2000000099",
            correo = "C2000000006@prueba.test", telefono = "3001234567", password = "clave-segura",
            autorizaTratamientoDatos = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("CorreoDuplicado", (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    private Task<HttpClient> Personal(string rol) => rol switch
    {
        "Operador" => api.OperadorAsync(),
        "Seguridad" => api.SeguridadAsync(),
        _ => api.AdministradorAsync(),
    };
}
