# server

Servidor ASP.NET Core (API, dominio, base de datos). Dueño: Alex (validación y cuentas; Jeisy revisa cuentas).

## Ejecutar

```
dotnet run --project TransitGate.Api
```

La API escucha en `http://0.0.0.0:5000` (contratos de integración §9), así que el ESP32 y la visión la alcanzan por la IP de la laptop. La primera vez Windows pregunta por el firewall: permitir en red **Privada**.

- Web (después de `npm run build` en `web/`): `http://localhost:5000/`
- Salud: `http://localhost:5000/api/salud`
- Documentación (Scalar): `http://localhost:5000/scalar`
- Pruebas: `dotnet test`

## Estado actual (semana 2)

Las rutas de los dispositivos siguen el Contrato API v1.1 (§6 y §7). La lectura ya pasa por el caso de uso `ValidarLectura` (Application), que valida torniquete, soporte y saldo en el orden de RF-20 a través del contrato C7 (`ISoporteResolver`, `ISaldoService`).

Mientras no hay base de datos, todo vive en memoria (`TransitGate.Api/Esqueleto`): un solo torniquete `tq_1`, y al reiniciar la API se pierde el estado. Las rutas del ESP32 y de la visión verifican `X-Device-Key` en modo tolerante (ver "Credenciales de los dispositivos" en [CAMBIOS.md](CAMBIOS.md)). Para pasar a la base de datos se cambia `AddEsqueletoEnMemoria` en `Program.cs`.

| Ruta | Qué hace hoy |
| --- | --- |
| `POST /api/torniquetes/{id}/lecturas` | Valida contra las tarjetas de prueba, debita la tarifa y abre un paso; repite la respuesta si llega el mismo `lecturaId` |
| `POST /api/torniquetes/{id}/latido` | Registra la señal de vida y confirma la apertura (`pasoIdAbierto`); devuelve el estado deseado y un desafío fijo |
| `POST /api/torniquetes/{id}/cruces` | Consume el paso, marca fraude o responde `duplicado` |
| `POST /api/torniquetes/{id}/vision/latido` | Registra la señal de vida de la visión; responde 204 |
| `GET /api/torniquetes` | Estado y conexión de cada torniquete (sin sesión todavía) |
| `GET /api/torniquetes/{id}/intentos` | Últimos 100 intentos de acceso (sin sesión todavía) |
| Hub `/hubs/eventos` | Evento `EstadoTorniquete` en vivo al grupo `operacion` (sin sesión todavía) |
| `GET /api/alertas?estado=` | Alertas de colado, opcionalmente por estado (sin sesión todavía) |
| `POST /api/alertas/{id}/apagar-alarma` | Apaga la alarma y deja la alerta Atendida (sin sesión todavía) |
| `GET /api/tarjetas/solicitudes?documento=` | Solicitudes de tarjeta física pendientes, la más antigua primero (sin sesión todavía) |
| `POST /api/torniquetes/{id}/modo-registro` | Pone el lector en modo registro para entregar la tarjeta de una solicitud (sin sesión todavía) |
| Cualquier otra ruta | La web compilada en `wwwroot` (`index.html`); una ruta desconocida bajo `/api` responde 404 |

Tarjetas de prueba (sección `DatosDePrueba` de `appsettings.json`, tarifa 3.200):

| UID | Estado | Saldo | Respuesta |
| --- | --- | --- | --- |
| `04A1B2C3` | Activa | 12.000 | Abre tres veces y luego `SaldoInsuficiente` |
| `DEADBEEF` | Activa | 2.000 | `SaldoInsuficiente` |
| `0BADCAFE` | Bloqueada | 50.000 | `SoporteBloqueado` |
| `5011C17A` | Entregada | 0 | `SoporteNoActivo` |
| cualquier otro | — | — | `TarjetaNoRegistrada` |

Solicitudes de prueba (sección `Solicitudes`): Luisa Rojas (documento `1032456789`, saldo 10.000) y Mateo Díaz (`1121987654`, saldo 0). Al ligarles un UID en modo registro, su tarjeta queda `Entregada` y responde `SoporteNoActivo` hasta que el cliente confirme la recepción.

Para probar con una tarjeta real, agrega su UID a esa lista. Los tiempos (expiración del paso, gracia de visión y plazo de conexión del ESP32) están en la sección `Operacion`.

## Mediciones

El log sale una línea por evento con hora UTC al milisegundo. Las líneas que empiezan con `RNF-04` registran cada cambio del estado deseado y las que empiezan con `RNF-06`, cada cruce con su retraso. Para guardar el log de una sesión de pruebas (PowerShell):

```
dotnet run --project TransitGate.Api | Tee-Object -FilePath mediciones.log
```

Lo que cambió y lo que necesita saber cada integrante está en [CAMBIOS.md](CAMBIOS.md).
