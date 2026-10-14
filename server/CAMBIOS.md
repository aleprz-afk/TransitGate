# Cambios del servidor: validación y torniquete

Responsable: Alex · Última actualización: 14 de octubre de 2026 · Todo está en `main` (hasta el PR #9). Los contratos están en `docs/`: Contrato de la API v1.2 y Contratos de integración v1.3

Este documento resume lo que ya hace la API y lo que cada uno necesita saber para integrarse. Se actualiza con cada entrega; el historial está al final.

## Resumen

- La API corre en `http://<IP de la laptop>:5000` y la documentación interactiva en `http://<IP>:5000/scalar`.
- Las rutas del ESP32 y de la visión siguen el Contrato API v1.1 (§6 y §7) y ya usan la máquina de estados real del torniquete: `Cerrado`, `EsperandoCruce`, `Alarma` y `ModoRegistro`.
- La lectura valida torniquete, soporte y saldo en el orden de RF-20, debita la tarifa (3.200) y abre un paso. Cada lectura deja un `IntentoAcceso` con su resultado y motivo (RF-23).
- El latido devuelve el estado deseado, registra la señal de vida y confirma la apertura del brazo.
- Los cruces consumen el paso, detectan colados y activan la alarma.
- El hub de SignalR `/hubs/eventos` envía `EstadoTorniquete` en vivo: estado, conexión del ESP32 y de la visión, y desincronización.
- Todo vive en memoria hasta que esté la base de datos (PostgreSQL en Docker, ver "Decisiones"): **al reiniciar la API se pierde el estado**.
- El ESP32 y la visión se identifican con la cabecera `X-Device-Key`. Por ahora en **modo tolerante**: no se rechaza nada, solo queda en el log.
- Hay 66 pruebas automatizadas (dominio y casos de uso), una por criterio de aceptación.

## Decisiones

### Base de datos: PostgreSQL local con Docker (13 de octubre)

**Qué:** PostgreSQL corre en un contenedor de Docker en la misma laptop que la API, tanto en desarrollo como en la demo. No se usa una base en la nube (Supabase u otra).

**Por qué:**

1. **La demo no puede depender de internet.** La arquitectura (§7 y §9) ya decidió que todo corre en la laptop y en la red local porque el internet de la universidad no es confiable. Con una base en la nube, sin internet el torniquete deja de validar.
2. **Los tiempos no alcanzan con una base remota.** RNF-03 da 200 ms al servidor para decidir un paso, y una lectura hace varias consultas (soporte, saldo, débito, intento y paso). Con la base en la nube cada consulta tarda 100 a 200 ms en ir y volver; con la base local tarda menos de 1 ms.
3. **Los planes gratuitos en la nube pausan el proyecto** tras unos días sin uso: un riesgo innecesario para la demo.
4. **PostgreSQL cumple los RNF de fiabilidad:** transacciones reales para guardar el paso completo (RNF-15) y control de concurrencia sobre el saldo (RNF-16).
5. **Docker iguala el entorno de todos:** misma versión y misma configuración con un solo comando. Si algún día se quiere publicar en la nube, es el mismo PostgreSQL: solo cambia la cadena de conexión.

**Cómo se usa** (lo prepara Jeisy, dueña de la base de datos):

- Requisito: Docker Desktop instalado y abierto.
- En la raíz del repo habrá un `docker-compose.yml` con PostgreSQL. La contraseña no va en el repo (RNF-25): va en un archivo `.env`, que git ya ignora, y en el repo se sube un `.env.example` con valores falsos. Propuesta:

  ```yaml
  services:
    db:
      image: postgres:17
      environment:
        POSTGRES_DB: transitgate
        POSTGRES_USER: transitgate
        POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      ports:
        - "5432:5432"
      volumes:
        - datos:/var/lib/postgresql/data
  volumes:
    datos:
  ```

- Levantar la base: `docker compose up -d`. Detenerla: `docker compose down`. **`docker compose down -v` borra todos los datos.**
- La cadena de conexión va en los secretos de usuario de .NET, no en `appsettings.json` (RNF-25):

  ```
  dotnet user-secrets set "ConnectionStrings:TransitGate" "Host=localhost;Port=5432;Database=transitgate;Username=transitgate;Password=<la del .env>" --project server/TransitGate.Api
  ```

- En la laptop de la demo, Docker Desktop tiene que estar abierto **antes** de arrancar la API.

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
- **Credencial:** envía la cabecera `X-Device-Key` con la clave del ESP32 en las lecturas y en los latidos (en `config.json` es `deviceKey`). Te paso la clave por privado: nunca va en el repo (RNF-25). Hasta después del H1 la API no rechaza nada; desde que se active el modo estricto, sin la clave correcta todo recibe 401 `CredencialInvalida`. Ver "Credenciales de los dispositivos" más abajo.
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
- **Credencial:** envía la cabecera `X-Device-Key` con la clave de la visión en los cruces y en los latidos; según `vision/config.yaml`, va en la variable de entorno `TG_VISION_KEY`. Es distinta de la del ESP32: la del ESP32 no sirve en las rutas de la visión, ni al revés. Mismo calendario que para Karol: tolerante hasta después del H1.
- **RNF-06:** cada cruce deja en el log de la API una línea `RNF-06` con la hora del fotograma, la hora de llegada y el retraso en milisegundos.

### Ana — web (interfaz C6)

- **SignalR:** hub `/hubs/eventos`, evento `EstadoTorniquete`. Son los campos del contrato C6 más dos horas para HU-20, `estadoDesde` y `ultimaSenal`:

  ```json
  { "torniqueteId": "tq_1", "estado": "Cerrado|EsperandoCruce|Alarma|ModoRegistro",
    "estadoDesde": "2026-10-14T15:29:23.002Z",
    "esp32Conectado": true, "ultimaSenal": "2026-10-14T15:29:22.736Z",
    "visionConectada": true, "desincronizado": false }
  ```

  - `estadoDesde`: desde cuándo está en ese estado, por ejemplo para mostrar "En alarma desde las 10:29" (HU-20 CA2). Es `null` si el torniquete no ha cambiado de estado desde que arrancó la API.
  - `ultimaSenal`: hora del último latido del ESP32, para mostrar "Fuera de línea desde…" cuando `esp32Conectado` pasa a `false` (HU-20 CA3). Es `null` si todavía no ha llegado ningún latido. Un latido nuevo **no** genera un evento: la hora solo viaja cuando cambia algo más.
  - Las dos horas vienen en UTC; se muestran en hora de Colombia con `Intl.DateTimeFormat` y `timeZone: "America/Bogota"` (RNF-42).
  - Al conectarse, el panel recibe enseguida el estado actual de cada torniquete; después, un evento por cada cambio. Los cambios llegan en menos de 1 s (RNF-05 pide 2 s).
  - Por ahora no exige sesión y toda conexión entra al grupo `operacion`.
  - Ejemplo con `@microsoft/signalr`:

  ```js
  const conexion = new signalR.HubConnectionBuilder().withUrl("/hubs/eventos").withAutomaticReconnect().build();
  conexion.on("EstadoTorniquete", (e) => console.log(e.estado, e.esp32Conectado));
  await conexion.start();
  ```

- `GET /api/torniquetes` devuelve `[{ id, nombre, estado, conectado, ultimaSenal }]` (Contrato §8), útil para la carga inicial.
- `GET /api/torniquetes/{id}/intentos` devuelve los últimos 100 intentos, del más reciente al más antiguo: `[{ intentoId, resultado, motivo, fecha }]` (Contrato §8, RF-32). `resultado` es `Exitoso`, `Rechazado` o `Fraude`; `motivo` no viene en los exitosos. Todavía no acepta `desde` ni `hasta`.
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
- `INotificador` (también de C7, lo implemento yo) está en `Application/Notificaciones/`. Por ahora solo tiene `EstadoTorniqueteAsync`, que recibe el estado ya calculado y no el id del torniquete que dice el contrato. Cuando agregue `SaldoActualizadoAsync` para tus recargas, te aviso.
- **Transacción del paso (RNF-15):** `ValidarLectura` termina con un solo `IUnidadDeTrabajo.GuardarCambiosAsync` (en `Application/`). Con EF Core, la idea es que tu `DbContext` lo implemente y que `ISaldoService` y mis repositorios compartan ese mismo `DbContext`, así el débito, el paso y el intento se guardan juntos o no se guarda ninguno.
- **Ids:** los pasos y los intentos piden su id antes de guardarse (`SiguientePasoIdAsync`, `SiguienteIdAsync`). En PostgreSQL esto se resuelve con secuencias (`UseHiLo` de Npgsql).
- Agregué el enum `TipoSoporte` (`Fisico`, `Virtual`) en `Domain/Tarjetas/` porque `IntentoAcceso` lo guarda (RF-23). También es de tu módulo.

## Credenciales de los dispositivos (RNF-20)

Cada torniquete tiene dos claves: una para su ESP32 y otra para su servicio de visión. La API las lee de los **secretos de usuario** de .NET, que se guardan fuera del repositorio (RNF-25).

**Generar una clave** (Git Bash):

```
openssl rand -hex 32
```

**Configurarlas en la laptop que corre la API** (desde la raíz del repo):

```
dotnet user-secrets set "Dispositivos:Claves:tq_1:Esp32" "<clave del ESP32>" --project server/TransitGate.Api
dotnet user-secrets set "Dispositivos:Claves:tq_1:Vision" "<clave de la visión>" --project server/TransitGate.Api
```

**Modo tolerante y modo estricto.** `Dispositivos:ExigirClave` en `appsettings.json`:

- `false` (ahora): toda petición pasa. Si falta la clave, es incorrecta o el torniquete no tiene clave configurada, queda **una** advertencia en el log (`X-Device-Key tq_1 Esp32: Ausente`), y otra cuando vuelve a ser válida. No se registra una por latido.
- `true` (después del H1): sin la clave correcta, 401 `{ "error": "CredencialInvalida" }`. Antes de activarlo hay que confirmar en el log que el ESP32 y la visión ya envían claves válidas.

Las rutas de monitoreo (`/api/torniquetes`, intentos, alertas) no usan esta clave: esas pedirán sesión de operador cuando esté Identity. Con RF-43, las claves se generarán al registrar el torniquete y se guardarán en la base de datos.

## Mediciones (RNF-03, RNF-04 y RNF-06)

El log tiene una línea por evento, con hora UTC al milisegundo:

```
RNF-03 POST /api/torniquetes/tq_1/lecturas 200 24.5 ms
RNF-04 tq_1 estado deseado: brazo=abierto sirena=apagada desde=2026-10-14T03:59:30.777Z causa=lectura
RNF-06 tq_1 cruce trackId=1 sentido=Entrada fotograma=2026-10-14T03:59:31.061Z llegada=2026-10-14T03:59:31.305Z retrasoMs=244 resultado=consumido
```

- **RNF-03:** cada petición a `/api` registra cuánto tardó dentro del servidor. Los latidos no aparecen salvo que fallen o pasen de 200 ms, para no llenar el log (llegan cada 300 ms). La medición oficial (p95 ≤ 200 ms con 1.000 tarjetas y 10.000 intentos) se hace cuando esté la base de datos; en memoria cada lectura tarda alrededor de 0,1 ms.

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
| `X-Device-Key` en modo tolerante | Se activa el modo estricto después del H1 (semana 3) |
| Las rutas del panel no piden sesión | Con Identity (Jeisy, semana 3) |
| La transacción del paso (RNF-15) está definida pero en memoria no se deshace nada si algo falla a la mitad | Cuando esté la base de datos; ahí se agrega la prueba con fallo forzado |
| Los cruces de fraude todavía no dejan un `IntentoAcceso` con resultado `Fraude` (RF-28 CA1) | Semana 4, cuando los cruces pasen a Application |
| Una lectura en modo registro se rechazaría como `TorniqueteOcupado` y dejaría un intento (RF-23 CA3 pide que no deje ninguno) | Semana 4, con la ruta del modo registro |
| No se revierte el débito si el brazo no abre (RF-46) | Semana 6 |
| SignalR solo envía `EstadoTorniquete`; faltan `IntentoRegistrado`, `AlertaNueva`, `SaldoActualizado` y los demás | Semanas 3 a 5, junto con cada entidad |
| Modo registro sin ruta todavía; el desafío del celular es fijo | Semanas 4 y Fase 4 |

## Historial

**14 de octubre (semana 2)**
- `X-Device-Key` en las rutas del ESP32 y de la visión (RNF-20), en modo tolerante hasta después del H1. Una clave por dispositivo y por torniquete, en los secretos de usuario.
- `EstadoTorniquete` trae `estadoDesde` y `ultimaSenal`, para que el panel muestre desde qué hora está en alarma y desde cuándo está fuera de línea (HU-20).

**13 de octubre (semana 2), tercera parte**
- Decisión: PostgreSQL local con Docker (sección "Decisiones").
- `IntentoAcceso` (RF-23): cada lectura queda registrada con resultado, motivo, tarjeta, UID leído y tarifa cobrada. Los reintentos con el mismo `lecturaId` se responden con el intento guardado.
- Transacción del paso definida con `IUnidadDeTrabajo` (RNF-15).
- `GET /api/torniquetes/{id}/intentos` para el historial del panel (RF-32).
- Registro del tiempo de cada petición para RNF-03.

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
