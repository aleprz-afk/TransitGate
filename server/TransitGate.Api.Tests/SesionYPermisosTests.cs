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
}
