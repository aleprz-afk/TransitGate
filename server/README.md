# server

Servidor ASP.NET Core (API, dominio, base de datos). Dueños: Alex (validación) y Jeisy (cuentas).

## Ejecutar

```
dotnet run --project TransitGate.Api
```

La API escucha en `http://0.0.0.0:5000` (contratos de integración §9), así que el ESP32 y la visión la alcanzan por la IP de la laptop. La primera vez Windows pregunta por el firewall: permitir en red **Privada**.

- Salud: `http://localhost:5000/api/salud`
- Documentación (Scalar): `http://localhost:5000/scalar`
- Pruebas: `dotnet test`

## Estado actual (semana 2)

Las rutas de los dispositivos siguen el Contrato API v1.1 (§6 y §7). La lectura ya pasa por el caso de uso `ValidarLectura` (Application), que valida torniquete, soporte y saldo en el orden de RF-20 a través del contrato C7 (`ISoporteResolver`, `ISaldoService`).

Mientras no hay base de datos, todo vive en memoria (`TransitGate.Api/Esqueleto`): un solo torniquete `tq_1`, sin `X-Device-Key`, y al reiniciar la API se pierde el estado. Para pasar a la base de datos se cambia `AddEsqueletoEnMemoria` en `Program.cs`.

| Ruta | Qué hace hoy |
| --- | --- |
| `POST /api/torniquetes/{id}/lecturas` | Valida contra las tarjetas de prueba, debita la tarifa y abre un paso; repite la respuesta si llega el mismo `lecturaId` |
| `POST /api/torniquetes/{id}/latido` | Devuelve el estado deseado real (brazo, sirena, paso pendiente) y un desafío fijo |
| `POST /api/torniquetes/{id}/cruces` | Consume el paso, marca fraude o responde `duplicado` |
| `POST /api/torniquetes/{id}/vision/latido` | Responde 204 |
| `POST /api/alertas/{id}/apagar-alarma` | Apaga la alarma (sin sesión todavía) |

Tarjetas de prueba (sección `DatosDePrueba` de `appsettings.json`, tarifa 3.200):

| UID | Estado | Saldo | Respuesta |
| --- | --- | --- | --- |
| `04A1B2C3` | Activa | 12.000 | Abre tres veces y luego `SaldoInsuficiente` |
| `DEADBEEF` | Activa | 2.000 | `SaldoInsuficiente` |
| `0BADCAFE` | Bloqueada | 50.000 | `SoporteBloqueado` |
| `5011C17A` | Entregada | 0 | `SoporteNoActivo` |
| cualquier otro | — | — | `TarjetaNoRegistrada` |

Para probar con una tarjeta real, agrega su UID a esa lista. Los tiempos (expiración del paso y gracia de visión) están en la sección `Operacion`.
