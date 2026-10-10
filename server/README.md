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

## API esqueleto (semana 1)

Las rutas de los dispositivos siguen el Contrato API v1.1 (§6 y §7) y ya usan la máquina de estados del torniquete (`TransitGate.Domain/Torniquetes`), pero todo vive en memoria: un solo torniquete `tq_1`, sin base de datos, sin `X-Device-Key` y con saldo fijo de 8.800. Al reiniciar la API se pierde el estado.

| Ruta | Qué hace hoy |
| --- | --- |
| `POST /api/torniquetes/{id}/lecturas` | Abre con cualquier UID válido; repite la respuesta si llega el mismo `lecturaId` |
| `POST /api/torniquetes/{id}/latido` | Devuelve el estado deseado real (brazo, sirena, paso pendiente) y un desafío fijo |
| `POST /api/torniquetes/{id}/cruces` | Consume el paso, marca fraude o responde `duplicado` |
| `POST /api/torniquetes/{id}/vision/latido` | Responde 204 |
| `POST /api/alertas/{id}/apagar-alarma` | Apaga la alarma (sin sesión todavía) |

UIDs de prueba para los rechazos:

| UID | Respuesta |
| --- | --- |
| `00000000` | `denegar`, `TarjetaNoRegistrada` |
| `DEADBEEF` | `denegar`, `SaldoInsuficiente` |

Los tiempos (expiración del paso y gracia de visión) están en la sección `Operacion` de `appsettings.json`.
