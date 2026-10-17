using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransitGate.Domain.Tarjetas;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Api.Tests;

[Collection(ColeccionApi.Nombre)]
public class FlujoClienteTests(ApiConBaseDeDatos api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    [Fact] // Guion del MVP, pasos 1 a 6 y 11 (H2)
    public async Task Registro_solicitud_entrega_recarga_paso_e_historial()
    {
        var cliente = await api.ClienteAsync("3000000001");
        var operador = await api.OperadorAsync();
        var esp32 = api.Navegador();

        // 2. Solicitud
        Assert.Equal(HttpStatusCode.Created, (await cliente.PostAsync("/api/tarjetas/mi-tarjeta/solicitud-fisica", null)).StatusCode);

        // 3. Entrega en taquilla y confirmación
        var solicitudes = await operador.GetFromJsonAsync<JsonElement>("/api/tarjetas/solicitudes?documento=3000000001");
        var solicitudId = solicitudes[0].GetProperty("solicitudId").GetString();
        Assert.Equal(HttpStatusCode.OK,
            (await operador.PostAsJsonAsync("/api/torniquetes/tq_1/modo-registro", new { solicitudId })).StatusCode);
        var registro = await Json(await esp32.PostAsJsonAsync("/api/torniquetes/tq_1/lecturas",
            new { lecturaId = "it-0001", tipo = "uid", valor = "1A2B3C4D" }));
        Assert.Equal("registrada", registro.GetProperty("accion").GetString());
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync("/api/tarjetas/mi-tarjeta/confirmar-recepcion", null)).StatusCode);

        // 4. Recarga
        var recarga = await Json(await cliente.PostAsJsonAsync("/api/tarjetas/mi-tarjeta/recargas", new { monto = 10000 }));
        Assert.Equal(10000, recarga.GetProperty("saldoNuevo").GetInt32());

        // 5. Paso
        var paso = await Json(await esp32.PostAsJsonAsync("/api/torniquetes/tq_1/lecturas",
            new { lecturaId = "it-0002", tipo = "uid", valor = "1A2B3C4D" }));
        Assert.Equal(("abrir", 6800), (paso.GetProperty("accion").GetString(), paso.GetProperty("saldoRestante").GetInt32()));

        // 11. Historial
        var viajes = await cliente.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta/viajes");
        Assert.Equal(1, viajes.GetArrayLength());
        Assert.Equal(("Exitoso", "Torniquete 1", 3200),
            (viajes[0].GetProperty("resultado").GetString(), viajes[0].GetProperty("torniqueteNombre").GetString(), viajes[0].GetProperty("tarifa").GetInt32()));
        var miTarjeta = await cliente.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta");
        Assert.Equal(6800, miTarjeta.GetProperty("saldo").GetInt32());
        Assert.Equal("Activa", miTarjeta.GetProperty("soporte").GetProperty("estado").GetString());
    }

    [Fact] // RF-15 CA5
    public async Task La_misma_clave_de_idempotencia_recarga_una_sola_vez()
    {
        var cliente = await api.ClienteAsync("3000000002");

        async Task<HttpResponseMessage> Recargar()
        {
            var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/tarjetas/mi-tarjeta/recargas")
            {
                Content = JsonContent.Create(new { monto = 10000 }),
            };
            peticion.Headers.Add("Idempotency-Key", "doble-clic");
            return await cliente.SendAsync(peticion);
        }

        await Task.WhenAll(Recargar(), Recargar());

        Assert.Equal(10000, (await cliente.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta")).GetProperty("saldo").GetInt32());
        Assert.Equal(1, (await cliente.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta/recargas")).GetArrayLength());
    }

    [Fact] // RNF-16: 20 recargas en paralelo
    public async Task Veinte_recargas_en_paralelo_dejan_el_saldo_exacto()
    {
        var cliente = await api.ClienteAsync("3000000003");

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            cliente.PostAsJsonAsync("/api/tarjetas/mi-tarjeta/recargas", new { monto = 1000 })));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(20000, (await cliente.GetFromJsonAsync<JsonElement>("/api/tarjetas/mi-tarjeta")).GetProperty("saldo").GetInt32());
    }

    [Fact] // RNF-15: si el guardado falla a la mitad, no queda nada
    public async Task Un_guardado_que_falla_no_deja_el_debito_a_medias()
    {
        await api.ClienteAsync("3000000004");
        Guid tarjetaId;
        using (var alcance = api.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<TransitGateDbContext>();
            var tarjeta = await db.Tarjetas.SingleAsync(t =>
                t.ClienteId == db.Clientes.Single(c => c.NumeroDocumento == "3000000004").Id);
            tarjeta.Acreditar(5000);
            db.Debitos.Add(new DebitoPaso(900_001, tarjeta.Id, 3200, DateTime.UtcNow));
            await db.SaveChangesAsync();
            tarjetaId = tarjeta.Id;
        }

        using (var alcance = api.Services.CreateScope())
        {
            // El débito de un paso nuevo y un DebitoPaso con un pasoId repetido: el segundo viola la llave.
            var db = alcance.ServiceProvider.GetRequiredService<TransitGateDbContext>();
            var tarjeta = await db.Tarjetas.SingleAsync(t => t.Id == tarjetaId);
            Assert.True(tarjeta.Debitar(3200));
            db.Debitos.Add(new DebitoPaso(900_001, tarjetaId, 3200, DateTime.UtcNow));

            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.GuardarCambiosAsync());
        }

        using (var alcance = api.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<TransitGateDbContext>();
            Assert.Equal(5000, (await db.Tarjetas.AsNoTracking().SingleAsync(t => t.Id == tarjetaId)).Saldo);
        }
    }
}
