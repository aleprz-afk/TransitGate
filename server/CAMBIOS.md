# Cambios del servidor

Responsable: Alex · Última actualización: 16 de octubre de 2026 · Todo está en `main` hasta el PR #12; en curso: `feat/modo-registro`. Los contratos están en `docs/`: Contrato de la API v1.3 y Contratos de integración v1.6

Este documento resume lo que ya hace la API y lo que cada uno necesita saber para integrarse. Se actualiza con cada entrega; el historial está al final.

## Resumen

- La API corre en `http://<IP de la laptop>:5000` y la documentación interactiva en `http://<IP>:5000/scalar`.
- Las rutas del ESP32 y de la visión siguen el Contrato API v1.1 (§6 y §7) y ya usan la máquina de estados real del torniquete: `Cerrado`, `EsperandoCruce`, `Alarma` y `ModoRegistro`.
- La lectura valida torniquete, soporte y saldo en el orden de RF-20, debita la tarifa (3.200) y abre un paso. Cada lectura deja un `IntentoAcceso` con su resultado y motivo (RF-23).
- El latido devuelve el estado deseado, registra la señal de vida y confirma la apertura del brazo.
- Los cruces consumen el paso, detectan colados y activan la alarma.
- **Entrega en taquilla:** el operador elige una solicitud de tarjeta física y pone el lector en modo registro; la siguiente tarjeta que se acerca queda ligada a esa solicitud, en `Entregada` (RF-09, RF-10, RF-36).
- El hub de SignalR `/hubs/eventos` envía `EstadoTorniquete` en vivo: estado, conexión del ESP32 y de la visión, y desincronización.
- Todo vive en memoria hasta que esté la base de datos (PostgreSQL en Docker, ver "Decisiones"): **al reiniciar la API se pierde el estado**.
- El ESP32 y la visión se identifican con la cabecera `X-Device-Key`. Por ahora en **modo tolerante**: no se rechaza nada, solo queda en el log.
- La API sirve la web (React) en el mismo puerto: `http://<IP>:5000/`. El proyecto de la web ya está en `web/`, con el proxy de desarrollo, los tipos generados desde el OpenAPI y una página de prueba del panel.
- Los números del JSON se reciben solo como números: `"pasoIdAbierto": "5"` (entre comillas) responde 400.
- Hay 89 pruebas automatizadas (dominio y casos de uso), una por criterio de aceptación.

## Decisiones

### Base de datos: PostgreSQL local con Docker (13 de octubre)

**Qué:** PostgreSQL corre en un contenedor de Docker en la misma laptop que la API, tanto en desarrollo como en la demo. No se usa una base en la nube (Supabase u otra).

**Por qué:**

1. **La demo no puede depender de internet.** La arquitectura (§7 y §9) ya decidió que todo corre en la laptop y en la red local porque el internet de la universidad no es confiable. Con una base en la nube, sin internet el torniquete deja de validar.
2. **Los tiempos no alcanzan con una base remota.** RNF-03 da 200 ms al servidor para decidir un paso, y una lectura hace varias consultas (soporte, saldo, débito, intento y paso). Con la base en la nube cada consulta tarda 100 a 200 ms en ir y volver; con la base local tarda menos de 1 ms.
3. **Los planes gratuitos en la nube pausan el proyecto** tras unos días sin uso: un riesgo innecesario para la demo.
4. **PostgreSQL cumple los RNF de fiabilidad:** transacciones reales para guardar el paso completo (RNF-15) y control de concurrencia sobre el saldo (RNF-16).
5. **Docker iguala el entorno de todos:** misma versión y misma configuración con un solo comando. Si algún día se quiere publicar en la nube, es el mismo PostgreSQL: solo cambia la cadena de conexión.

**Cómo se usa** (llega con el tercer PR del módulo de cuentas):

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

### Alex toma el módulo de cuentas, tarjetas y saldo (16 de octubre)

**Qué:** el módulo de cuentas (C7: `Cliente`, `Tarjeta`, `SoporteTarjeta`, `Recarga`, `Tarifa`, `Operador`), la base de datos y la sesión con Identity los construye Alex. Jeisy revisa los PR de ese módulo y sigue con la app Android (C2, C5).

**Por qué:**

1. El H2 (30 de octubre) muestra registro, solicitud, entrega, recarga e historial, y todo eso depende de este módulo, que aún no había empezado.
2. El backend de validación ya está adelantado y comparte las mismas interfaces (C7), así que el cambio no rompe nada de lo construido.
3. Una sola persona en la base de datos evita migraciones cruzadas en estas dos semanas.

**Cómo se hace:** en tres PR seguidos: (1) modo registro y taquilla, (2) cuentas, tarjetas y saldo en memoria, (3) PostgreSQL con Docker, Identity y las rutas del cliente. La separación entre módulos se mantiene: validación solo habla con cuentas por las interfaces de C7.

## Qué necesita saber cada uno

### Karol — firmware del ESP32 (interfaz C1)

- **Dirección:** `http://<IP de la laptop>:5000`. La primera vez que se corre la API, Windows pregunta por el firewall: hay que permitirla en red **Privada**.
- **Lectura** `POST /api/torniquetes/tq_1/lecturas`:
  - El UID va en hexadecimal **mayúsculas**, con 8 o 14 caracteres. Si no, responde 400 `UidInvalido`.
  - Si se reenvía el mismo `lecturaId`, responde lo mismo y no cobra otra vez.
  - Solo abren las tarjetas de la tabla de abajo. Una tarjeta real responde `TarjetaNoRegistrada` hasta que me pases su UID y la agregue.
- **Latido** `POST /api/torniquetes/tq_1/latido`:
  - Campos obligatorios: `brazoReal` (`"abierto"` o `"cerrado"`), `sirenaReal` y `arranque`. Si falta alguno, responde 400.
  - Cuando el brazo abre para un paso, manda su id en `pasoIdAbierto` en el siguiente latido; así el servidor sabe que abrió (base de RF-46). Va como número (`"pasoIdAbierto": 5`), no entre comillas: entre comillas responde 400.
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

- **Modo registro** (entrega de tarjetas en taquilla): mientras el latido responda `"modoRegistro": true`, la lectura no es un paso y el brazo no se mueve. Puedes encender un LED. Las respuestas posibles:

  ```json
  { "accion": "registrada", "lecturaId": "a1f3-0007" }    // tarjeta ligada; el modo registro termina
  { "accion": "denegar", "motivo": "UidYaRegistrado" }     // esa tarjeta ya es de alguien; sigue en modo registro
  ```

  Con `registrada` no hay que abrir el brazo ni mandar `pasoIdAbierto`. El reintento con el mismo `lecturaId` recibe la misma respuesta.

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

- **Proyecto listo en `web/`** (Vite + React + TypeScript). Para empezar: API arriba en el 5000, luego `npm install` y `npm run dev` en `web/`, y abrir `http://localhost:5173`. El proxy de Vite reenvía `/api`, `/hubs`, `/openapi` y `/scalar` a la API, así que no hace falta CORS. Los detalles están en `web/README.md`.
- **Tipos:** `src/api/esquema.d.ts` se genera con `npm run tipos` (con la API arriba) y está en el repositorio. `src/api/cliente.ts` exporta `api`, un cliente tipado: `api.GET('/api/torniquetes')`, `api.POST('/api/alertas/{id}/apagar-alarma', { params: { path: { id } } })`. Los enteros (`intentoId`, `pasoId`) salen como `number`.
- **SignalR:** `src/api/eventos.ts` tiene `conectarEventos({ EstadoTorniquete, IntentoRegistrado, AlertaNueva, AlertaActualizada })` y los tipos de cada evento, escritos a mano porque el OpenAPI no describe el hub. Si cambia un evento, te aviso y se cambian ahí.
- **Página de prueba:** `src/App.tsx` muestra los torniquetes, las alertas abiertas (con el botón de apagar) y los intentos en vivo. Está probada con lecturas, un colado y apagando la alarma; también en 375 px de ancho. Es para reemplazarla por las páginas reales (no hay router todavía: elige tú la librería).
- **Demo:** `npm run build` deja la web en `server/TransitGate.Api/wwwroot`, que no se sube al repositorio. La API la sirve en `http://<IP>:5000/` y devuelve `index.html` para cualquier ruta que no sea de la API, así funcionan las rutas de React Router al recargar. Si `wwwroot` no existía cuando arrancó la API, hay que reiniciarla.
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

- **Taquilla (RF-36, RF-09, RF-10):**
  - `GET /api/tarjetas/solicitudes` devuelve las solicitudes pendientes, la más antigua primero: `[{ solicitudId, clienteId, nombreCompleto, documento, fecha }]`. El `documento` viene con solo los 4 últimos dígitos (`••••••6789`), salvo al buscar con `?documento=1032456789`, que lo devuelve completo (RNF-27). Si la búsqueda no encuentra nada, la lista viene vacía: ahí va el mensaje "Sin solicitud pendiente" (RF-36 CA2).
  - `POST /api/torniquetes/tq_1/modo-registro` con `{ "solicitudId": "s_…" }` responde `{ "expiraEn": 60 }` (botón "Entregar", RF-36 CA3). Si el torniquete no está en `Cerrado` responde 409 `TorniqueteOcupado`; si la solicitud ya se entregó, 404 `SolicitudNoEncontrada`.
  - Mientras dura, `EstadoTorniquete` llega con `estado: "ModoRegistro"`: ahí va el "Acerque la tarjeta" (RF-09 CA1). Si pasan 60 s sin tarjeta vuelve a `Cerrado` y la solicitud sigue en la lista.
  - El resultado de la lectura llega en el evento `RegistroTarjeta` (tipo en `src/api/eventos.ts`):

    ```json
    RegistroTarjeta { "torniqueteId": "tq_1", "solicitudId": "s_…", "resultado": "Ligado|UidYaRegistrado|SolicitudNoPendiente", "fecha": "…" }
    ```

    `Ligado` → "Tarjeta entregada" y la solicitud sale de la lista (CA4); `UidYaRegistrado` → "Tarjeta ya registrada", el torniquete sigue esperando otra tarjeta (RF-10 CA2).
  - Hay dos solicitudes de prueba: Luisa Rojas (`1032456789`) y Mateo Díaz (`1121987654`).

- **Más eventos en el grupo `operacion`**, con el contenido del contrato C6:

  ```json
  IntentoRegistrado { "intentoId": 3, "torniqueteId": "tq_1", "resultado": "Exitoso|Rechazado|Fraude",
                      "motivo": "SaldoInsuficiente", "fecha": "…" }
  AlertaNueva       { "alertaId": "al_1", "torniqueteId": "tq_1", "tipo": "FraudeSinValidacion", "fecha": "…" }
  AlertaActualizada { "alertaId": "al_1", "estado": "Atendida" }
  ```

  - `IntentoRegistrado` llega con cada lectura y con cada colado, para agregar el intento al inicio de la lista sin recargar (HU-19 CA1). `motivo` es `null` en los exitosos.
  - `AlertaNueva` llega solo con el primer colado; los siguientes, mientras la alarma suena, se suman a la misma alerta y llegan solo como `IntentoRegistrado` de fraude (HU-21 CA3).
  - `AlertaActualizada` llega al apagar la alarma.

- `GET /api/torniquetes` devuelve `[{ id, nombre, estado, conectado, ultimaSenal }]` (Contrato §8), útil para la carga inicial.
- `GET /api/torniquetes/{id}/intentos` devuelve los últimos 100 intentos, del más reciente al más antiguo: `[{ intentoId, resultado, motivo, fecha }]` (Contrato §8, RF-32). `resultado` es `Exitoso`, `Rechazado` o `Fraude`; `motivo` no viene en los exitosos. Todavía no acepta `desde` ni `hasta`.
- `GET /api/alertas?estado=Abierta` devuelve `[{ alertaId, torniqueteId, estado, fecha }]`, las más recientes primero (RF-33); sin `estado` devuelve todas. Un estado que no sea `Abierta`, `Atendida` o `Resuelta` recibe 400.
- `POST /api/alertas/{alertaId}/apagar-alarma` apaga la alarma: el torniquete vuelve a `Cerrado` y la alerta queda `Atendida` (HU-22 CA1). Para el MVP con eso basta; clasificarla y dejarla `Resuelta` (RF-35) va en la entrega final. Apagar una alarma ya apagada responde 200 sin hacer nada.
- Estas rutas funcionan **sin sesión** por ahora; exigirán sesión de operador cuando esté Identity.
- El OpenAPI para generar los tipos TypeScript está en `http://<IP>:5000/openapi/v1.json`.

### Jeisy — revisión del módulo de cuentas (interfaz C7)

- Desde el 16 de octubre el módulo de cuentas, la base de datos e Identity los construyo yo (ver "Decisiones"). Te pido que revises esos PR: eres revisora de C7, y si ya tenías algo avanzado sin subir, avísame para no repetirlo.
- Lo que cambió en C7 (contratos de integración v1.6): nueva interfaz `ISolicitudesTarjeta` en `Application/Cuentas/` para la entrega en taquilla (`PendientePorIdAsync`, `PendientesAsync`, `LigarUidAsync`). Una solicitud es el `SoporteTarjeta` físico en `Solicitada`; su id es el del soporte.
- Por ahora `CuentasDePrueba` (en memoria, en `TransitGate.Api/Esqueleto/`) implementa todo C7. En el siguiente PR la reemplazan el dominio real (`Cliente`, `Tarjeta`, `SoporteTarjeta`, `Recarga`) y sus casos de uso.
- Decisiones de C7 que siguen vigentes: `EstadoSoporte` y `TipoSoporte` están en `Domain/Tarjetas/`; `ResultadoDebito` es `(bool Exitoso, decimal SaldoRestante)` y si el saldo no alcanza no se descuenta nada (S3); el dinero es `decimal` adentro y entero en el JSON; el id del paso se reserva antes de debitar; el débito, el paso y el intento se guardan con un solo `IUnidadDeTrabajo.GuardarCambiosAsync` (RNF-15).
- Para la app (C5) no cambia nada todavía: las rutas `/api/auth/*` y `/api/tarjetas/mi-tarjeta/soporte-virtual` llegan con Identity.

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
| Todo en memoria; se pierde al reiniciar | Con PostgreSQL (tercer PR del módulo de cuentas) |
| `X-Device-Key` en modo tolerante | Se activa el modo estricto después del H1 (semana 3) |
| Las rutas del panel y de la taquilla no piden sesión | Con Identity (tercer PR del módulo de cuentas) |
| La transacción del paso (RNF-15) está definida pero en memoria no se deshace nada si algo falla a la mitad | Cuando esté la base de datos; ahí se agrega la prueba con fallo forzado |
| No se revierte el débito si el brazo no abre (RF-46) | Semana 6 |
| SignalR todavía no envía los eventos del cliente (`SaldoActualizado`, `EventoViaje`) ni `SolicitudNueva` | `SolicitudNueva` y `SaldoActualizado` con el segundo PR del módulo de cuentas; `EventoViaje` con Identity |
| Quién apagó la alarma no queda registrado (HU-22 CA1 pide "con mi nombre") | Con Identity |
| El desafío del celular es fijo | Fase 4 |
| Una tarjeta entregada en taquilla no se puede activar todavía: responde `SoporteNoActivo` hasta que exista "confirmar recepción" (RF-11) | Siguiente PR (cuentas, tarjetas y saldo) |
| Las solicitudes de tarjeta son datos de prueba fijos; todavía no se pueden crear desde el portal (RF-08) | Siguiente PR (cuentas, tarjetas y saldo) |

## Historial

**16 de octubre (semana 2), segunda parte**
- Entrega de tarjetas en taquilla: `GET /api/tarjetas/solicitudes` (RF-36) y `POST /api/torniquetes/{id}/modo-registro` (RF-09). La lectura en modo registro liga el UID a la solicitud y la deja `Entregada` (RF-10); un UID ya registrado no se liga y el torniquete sigue esperando otra tarjeta.
- Una lectura en modo registro ya no deja `IntentoAcceso` (RF-23 CA3).
- Evento `RegistroTarjeta` para el panel.
- Alex toma el módulo de cuentas, tarjetas y saldo; Jeisy queda como revisora (Contratos de integración v1.6, C7). Contrato de la API v1.3.

**16 de octubre (semana 2)**
- Proyecto de la web en `web/` (Vite + React + TypeScript): proxy hacia la API, tipos generados desde el OpenAPI, conexión a SignalR y una página de prueba del panel.
- La API sirve la web compilada en `wwwroot` (`UseStaticFiles` y `MapFallbackToFile`); las rutas desconocidas bajo `/api` siguen respondiendo 404.
- Los números del JSON se reciben solo como números (antes también se aceptaban entre comillas); así el OpenAPI los publica como `number`.
- Contratos de integración v1.5 (C6).

**14 de octubre (semana 2), segunda parte**
- Cruces y alertas pasan a casos de uso de Application: cada cruce queda registrado (RF-26 CA1); un colado abre una `AlertaSeguridad` o se suma a la abierta, y deja un `IntentoAcceso` de fraude (RF-28).
- Apagar la alarma deja la alerta `Atendida` (RF-34).
- `GET /api/alertas?estado=` (RF-33).
- Eventos `IntentoRegistrado`, `AlertaNueva` y `AlertaActualizada` para el panel.
- Ya no queda lógica en el esqueleto: en `TransitGate.Api/Esqueleto` solo quedan los almacenes en memoria.

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
