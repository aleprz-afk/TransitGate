# Cambios del servidor: validación y torniquete

Responsable: Alex · Última actualización: 13 de octubre de 2026 · Ramas: `feat/rf-20-validacion` (PR #2) y `feat/signalr-estado-torniquete`

Este documento resume lo que ya hace la API y lo que cada uno necesita saber para integrarse. Se actualiza con cada entrega; el historial está al final.

## Resumen

- La API corre en `http://<IP de la laptop>:5000` y la documentación interactiva en `http://<IP>:5000/scalar`.
- Las rutas del ESP32 y de la visión siguen el Contrato API v1.1 (§6 y §7) y ya usan la máquina de estados real del torniquete: `Cerrado`, `EsperandoCruce`, `Alarma` y `ModoRegistro`.
- La lectura valida torniquete, soporte y saldo en el orden de RF-20, debita la tarifa (3.200) y abre un paso.
- El latido devuelve el estado deseado, registra la señal de vida y confirma la apertura del brazo.
- Los cruces consumen el paso, detectan colados y activan la alarma.
- El hub de SignalR `/hubs/eventos` envía `EstadoTorniquete` en vivo: estado, conexión del ESP32 y de la visión, y desincronización.
- Todo vive en memoria hasta que esté la base de datos: **al reiniciar la API se pierde el estado**.
- Hay 52 pruebas automatizadas (dominio y casos de uso), una por criterio de aceptación.

## Qué necesita saber cada uno

### Karol — firmware del ESP32 (interfaz C1)

- **Dirección:** `http://<IP de la laptop>:5000`. La primera vez que se corre la API, Windows pregunta por el firewall: hay que permitirla en red **Privada**.
- **Lectura** `POST /api/torniquetes/tq_1/lecturas`:
  - El UID va en hexadecimal **mayúsculas**, con 8 o 14 caracteres. Si no, responde 400 `UidInvalido`.
  - Si se reenvía el mismo `lecturaId`, responde lo mismo y no cobra otra vez.
  - Solo abren las tarjetas de la tabla de abajo. Una tarjeta real responde `TarjetaNoRegistrada` hasta que me pases su UID y la agregue.
- **Latido** `POST /api/torniquetes/tq_1/latido`:
  - Campos obligatorios: `brazoReal` (`"abierto"` o `"cerrado"`), `sirenaReal` y `arranque`. Si falta alguno, responde 400.
  - Cuando el brazo abre para un paso, manda su id en `pasoIdAbierto` en el siguiente latido; así el servidor sabe que abrió (base de RF-46).
  - Genera un `arranque` nuevo en cada reinicio: el servidor deja registrado cada reinicio en el log.
  - Tras 5 s sin latidos, el torniquete figura **sin conexión**.
  - `brazoReal` y `sirenaReal` deben ser lo que el ESP32 aplicó de verdad: si no coinciden con lo deseado durante más de 2 s, el panel marca el torniquete como **desincronizado** (RF-45 CA4).
- **Credencial:** `X-Device-Key` todavía no se exige, pero conviene enviarla desde ya; se activa cuando los torniquetes estén en la base de datos.
- **Tarjetas de prueba** (tarifa 3.200):

| UID | Estado | Saldo | Respuesta |
| --- | --- | --- | --- |
| `04A1B2C3` | Activa | 12.000 | Abre tres veces y luego `SaldoInsuficiente` |
| `DEADBEEF` | Activa | 2.000 | `SaldoInsuficiente` |
| `0BADCAFE` | Bloqueada | 50.000 | `SoporteBloqueado` |
| `5011C17A` | Entregada | 0 | `SoporteNoActivo` |
| cualquier otro | — | — | `TarjetaNoRegistrada` |

### Wili — servicio de visión (interfaz C4)

- **Cruce** `POST /api/torniquetes/tq_1/cruces`:
  - `horaFotograma` va en UTC con formato ISO-8601 **terminado en `Z`** (por ejemplo `2026-10-13T20:30:01.450Z`). Si no, responde 400 `HoraFotogramaInvalida`.
  - El mismo `sesionVision` + `trackId` reportado dos veces responde `duplicado`.
  - Un cruce de `salida` responde `registrado`: no consume pasos ni es fraude.
  - Un cruce de entrada consume el paso si su fotograma cae dentro de los 10 s de la ventana. Sin paso disponible, responde `fraude` con un `alertaId`.
- **Latido** `POST /api/torniquetes/tq_1/vision/latido` cada 5 s, con `fps` y `sesionVision`. Tras 15 s sin señal, el panel muestra la visión **fuera de línea**.
- **RNF-06:** cada cruce deja en el log de la API una línea `RNF-06` con la hora del fotograma, la hora de llegada y el retraso en milisegundos.

### Ana — web (interfaz C6)

- **SignalR:** hub `/hubs/eventos`, evento `EstadoTorniquete` con el formato del contrato C6:

  ```json
  { "torniqueteId": "tq_1", "estado": "Cerrado|EsperandoCruce|Alarma|ModoRegistro",
    "esp32Conectado": true, "visionConectada": true, "desincronizado": false }
  ```

  - Al conectarse, el panel recibe enseguida el estado actual de cada torniquete; después, un evento por cada cambio. Los cambios llegan en menos de 1 s (RNF-05 pide 2 s).
  - Por ahora no exige sesión y toda conexión entra al grupo `operacion`.
  - Ejemplo con `@microsoft/signalr`:

  ```js
  const conexion = new signalR.HubConnectionBuilder().withUrl("/hubs/eventos").withAutomaticReconnect().build();
  conexion.on("EstadoTorniquete", (e) => console.log(e.estado, e.esp32Conectado));
  await conexion.start();
  ```

- `GET /api/torniquetes` devuelve `[{ id, nombre, estado, conectado, ultimaSenal }]` (Contrato §8), útil para la carga inicial.
- `POST /api/alertas/{alertaId}/apagar-alarma` apaga la alarma y cierra la alerta.
- Estas rutas funcionan **sin sesión** por ahora; exigirán sesión de operador cuando esté Identity.
- El OpenAPI para generar los tipos TypeScript está en `http://<IP>:5000/openapi/v1.json`.

### Jeisy — cuentas, tarjetas y saldo (interfaz C7)

- Las interfaces del contrato C7 están en `server/TransitGate.Application/Cuentas/`: `ISoporteResolver` e `ISaldoService`, copiadas del documento de integración.
- Mientras no estén tus implementaciones, la API usa `CuentasDePrueba` (en memoria, en `TransitGate.Api/Esqueleto/`). Para pasar a la base de datos se cambia la línea `AddEsqueletoEnMemoria` de `Program.cs` por el registro de Infrastructure.
- Tres decisiones que el contrato no definía y que conviene revisar juntos:
  1. Creé el enum `EstadoSoporte` (`Solicitada`, `Entregada`, `Activa`, `Bloqueada`) en `Domain/Tarjetas/`. Es de tu módulo: muévelo o ajústalo si lo necesitas.
  2. `ResultadoDebito` quedó como `(bool Exitoso, decimal SaldoRestante)`. Si el saldo no alcanza, no se descuenta nada (S3).
  3. C7 usa `decimal` para el dinero y el JSON usa enteros (RNF-24). Hoy la API convierte a entero al responder.
- El id del paso se reserva **antes** de debitar, porque `DebitarAsync` lo recibe.
- `INotificador` (también de C7, lo implemento yo) está en `Application/Notificaciones/`. Por ahora solo tiene `EstadoTorniqueteAsync`, y recibe el estado ya calculado en lugar del id del torniquete, como dice el contrato. Cuando agregue `SaldoActualizadoAsync` para tus recargas, te aviso.

## Mediciones de H1 (RNF-04 y RNF-06)

El log tiene una línea por evento, con hora UTC al milisegundo:

```
RNF-04 tq_1 estado deseado: brazo=abierto sirena=apagada desde=2026-10-14T03:59:30.777Z causa=lectura
RNF-06 tq_1 cruce trackId=1 sentido=Entrada fotograma=2026-10-14T03:59:31.061Z llegada=2026-10-14T03:59:31.305Z retrasoMs=244 resultado=consumido
```

- **RNF-04:** se compara `desde` con la hora en que el ESP32 aplica el cambio (en su propio log, con NTP). Cuando un paso vence, `desde` es la hora en que vencía el plazo, no la hora en que el servidor lo notó.
- **RNF-06:** `retrasoMs` cubre del servicio de visión a la API. El tramo de la cámara al servicio se mide con el cronómetro filmado.
- Para guardar el log de una sesión (PowerShell, desde `server/`):

```
dotnet run --project TransitGate.Api | Tee-Object -FilePath mediciones.log
```

## Limitaciones actuales

| Limitación | Cuándo se resuelve |
| --- | --- |
| Todo en memoria; se pierde al reiniciar | Cuando esté la base de datos (Jeisy, semana 2) |
| Sin `X-Device-Key` ni sesión en el panel | Con la base de datos e Identity (semanas 2 y 3) |
| No se registra el `IntentoAcceso` ni hay transacción del paso (RNF-15) | Semana 3 |
| No se revierte el débito si el brazo no abre (RF-46) | Semana 6 |
| SignalR solo envía `EstadoTorniquete`; faltan `IntentoRegistrado`, `AlertaNueva`, `SaldoActualizado` y los demás | Semanas 3 a 5, junto con cada entidad |
| `INotificador.EstadoTorniqueteAsync` no coincide con la firma del contrato C7 | Actualizar el documento de integración en un PR con la etiqueta `contrato` |
| Modo registro sin ruta todavía; el desafío del celular es fijo | Semanas 4 y Fase 4 |

## Historial

**13 de octubre (semana 2), segunda parte**
- Hub de SignalR `/hubs/eventos` con el evento `EstadoTorniquete`.
- Revisión de los torniquetes cada 500 ms: cierra los pasos vencidos sin esperar un latido y detecta cuándo el ESP32 o la visión dejan de responder.
- Desincronización: aviso cuando el ESP32 no aplica el estado deseado durante más de 2 s (RF-45 CA4).

**13 de octubre (semana 2)**
- Caso de uso de validación de la lectura con las interfaces C7 y tarjetas de prueba configurables.
- Latido real: señal de vida, `arranque` y confirmación de apertura.
- `GET /api/torniquetes` con estado y conexión.
- Log con hora al milisegundo para RNF-04 y RNF-06.

**9 y 10 de octubre (semana 1)**
- Máquina de estados del torniquete en el dominio, con sus pruebas.
- API en el puerto 5000 con las rutas del ESP32 y de la visión, en memoria.
