# Cambios del servidor

Responsable: Alex · Última actualización: 30 de octubre de 2026 · Todo está en `main` hasta el PR #24; en curso: `feat/web-estilo`. Los contratos están en `docs/`: Contrato de la API v1.5 y Contratos de integración v1.10

Este documento resume lo que ya hace la API y lo que cada uno necesita saber para integrarse. Se actualiza con cada entrega; el historial está al final.

## Resumen

- La API corre en `http://<IP de la laptop>:5000` y la documentación interactiva en `http://<IP>:5000/scalar`.
- Las rutas del ESP32 y de la visión siguen el Contrato API v1.1 (§6 y §7) y ya usan la máquina de estados real del torniquete: `Cerrado`, `EsperandoCruce`, `Alarma` y `ModoRegistro`.
- La lectura valida torniquete, soporte y saldo en el orden de RF-20, debita la tarifa (3.200) y abre un paso. Cada lectura deja un `IntentoAcceso` con su resultado y motivo (RF-23).
- El latido devuelve el estado deseado, registra la señal de vida y confirma la apertura del brazo.
- Los cruces consumen el paso, detectan colados y activan la alarma.
- **Entrega en taquilla:** el operador elige una solicitud de tarjeta física y pone el lector en modo registro; la siguiente tarjeta que se acerca queda ligada a esa solicitud, en `Entregada` (RF-09, RF-10, RF-36).
- El hub de SignalR `/hubs/eventos` envía `EstadoTorniquete` en vivo: estado, conexión del ESP32 y de la visión, y desincronización.
- **Portal del cliente:** registro, inicio de sesión, solicitud de la tarjeta física, confirmación de la recepción, recargas con pago simulado, saldo e historiales (Contrato §2 a §5 y §9).
- **Base de datos:** todo se guarda en PostgreSQL (Docker, en la misma laptop): **reiniciar la API ya no borra nada**. Solo el estado de cada torniquete vive en memoria y arranca en `Cerrado`. La API necesita Docker Desktop abierto y su cadena de conexión (ver "Base de datos" en `server/README.md`).
- **Sesión:** el panel, la taquilla y el portal piden sesión (cookie de Identity); sin sesión, 401. Hay una cuenta semilla de operador y otra de administrador.
- El ESP32 y la visión se identifican con la cabecera `X-Device-Key`. Por ahora en **modo tolerante**: no se rechaza nada, solo queda en el log.
- **Web completa para el MVP:** portal del cliente (registro, mi tarjeta, recargar, viajes, recargas, perfil) y panel del personal (panel en vivo y taquilla), servidos por la API en `http://<IP>:5000/`. Pensada para el celular.
- Los números del JSON se reciben solo como números: `"pasoIdAbierto": "5"` (entre comillas) responde 400.
- **Firmware del ESP32 principal** (`firmware/esp32-main/`, MicroPython): reglas de C1, cliente HTTP con timeouts, WiFi, servo, sirena, LED, el lector PN532 (ya probado con el módulo real) y uno simulado; cargado en una ESP32 real.
- **Firmware del ESP32-CAM** (`firmware/esp32-cam/`, Arduino): transmite MJPEG en `http://<ip>:81/stream` (C3). Probado en la placa por WiFi: OV3660, VGA, 24,6 FPS.
- **`sim-esp32`** (`tools/sim-esp32/`): simula la placa en el PC con el mismo código del firmware. Con la web y la API alcanza para demostrar el H2 sin hardware.
- **Servicio de visión** (`vision/`, Python + YOLO + ByteTrack): detecta personas en el stream de la CAM, cuenta los cruces de la línea virtual y los reporta a la API con su latido (C4). Probado contra la API real; falta la prueba con personas en vista cenital.
- Hay 218 pruebas automatizadas: 153 del servidor (dominio, casos de uso y 16 de integración contra un PostgreSQL de verdad), 33 del firmware y 32 de la visión.

## Decisiones

### Base de datos: PostgreSQL local con Docker (13 de octubre)

**Qué:** PostgreSQL corre en un contenedor de Docker en la misma laptop que la API, tanto en desarrollo como en la demo. No se usa una base en la nube (Supabase u otra).

**Por qué:**

1. **La demo no puede depender de internet.** La arquitectura (§7 y §9) ya decidió que todo corre en la laptop y en la red local porque el internet de la universidad no es confiable. Con una base en la nube, sin internet el torniquete deja de validar.
2. **Los tiempos no alcanzan con una base remota.** RNF-03 da 200 ms al servidor para decidir un paso, y una lectura hace varias consultas (soporte, saldo, débito, intento y paso). Con la base en la nube cada consulta tarda 100 a 200 ms en ir y volver; con la base local tarda menos de 1 ms.
3. **Los planes gratuitos en la nube pausan el proyecto** tras unos días sin uso: un riesgo innecesario para la demo.
4. **PostgreSQL cumple los RNF de fiabilidad:** transacciones reales para guardar el paso completo (RNF-15) y control de concurrencia sobre el saldo (RNF-16).
5. **Docker iguala el entorno de todos:** misma versión y misma configuración con un solo comando. Si algún día se quiere publicar en la nube, es el mismo PostgreSQL: solo cambia la cadena de conexión.

**Cómo se usa:** los pasos están en "Base de datos" de `server/README.md`: copiar `.env.example` a `.env`, `docker compose up -d` y la cadena de conexión en los secretos de usuario. La API aplica las migraciones sola al arrancar. En la laptop de la demo, Docker Desktop tiene que estar abierto **antes** de arrancar la API. **`docker compose down -v` borra todos los datos.**

### El estado del torniquete vive en memoria (16 de octubre)

**Qué:** con la base de datos, todo se guarda en PostgreSQL salvo la máquina de estados de cada torniquete (`Cerrado`, `EsperandoCruce`, `Alarma`, `ModoRegistro`, el paso pendiente y las últimas señales).

**Por qué:**

1. El ESP32 la consulta cada 300 ms y la API la protege con un cerrojo por torniquete; leerla de la base en cada latido no aporta nada.
2. Al reiniciar la API, arrancar en `Cerrado` es justo lo que pide RF-45 CA3: el brazo cerrado y la sirena apagada.
3. Lo que sí importa conservar se guarda: intentos, cruces, alertas, débitos y saldos. Los ids de los pasos salen de una secuencia de PostgreSQL, así no se repiten tras un reinicio.

**Qué se pierde al reiniciar:** un paso pendiente (el brazo cierra y la persona vuelve a pasar la tarjeta) y una alarma sonando (la alerta sigue `Abierta` en la base).

### Alex toma el módulo de cuentas, tarjetas y saldo (16 de octubre)

**Qué:** el módulo de cuentas (C7: `Cliente`, `Tarjeta`, `SoporteTarjeta`, `Recarga`, `Tarifa`, `Operador`), la base de datos y la sesión con Identity los construye Alex. Jeisy revisa los PR de ese módulo y sigue con la app Android (C2, C5).

**Por qué:**

1. El H2 (30 de octubre) muestra registro, solicitud, entrega, recarga e historial, y todo eso depende de este módulo, que aún no había empezado.
2. El backend de validación ya está adelantado y comparte las mismas interfaces (C7), así que el cambio no rompe nada de lo construido.
3. Una sola persona en la base de datos evita migraciones cruzadas en estas dos semanas.

**Cómo se hace:** en tres PR seguidos: (1) modo registro y taquilla, (2) cuentas, tarjetas y saldo en memoria, (3) PostgreSQL con Docker, Identity y las rutas del cliente. La separación entre módulos se mantiene: validación solo habla con cuentas por las interfaces de C7.

## Qué necesita saber cada uno

### Karol — firmware del ESP32 (interfaz C1)

- **Firmware empezado en `firmware/esp32-main/` (17 de octubre):** lo armé con la ESP32 que tengo, ya con MicroPython 1.29, para no llegar al H2 sin nada. Tiene las reglas de C1 en `controlador.py` (con 12 pruebas en el PC), el cliente HTTP con timeouts (`api.py`, ya probado contra la API), el WiFi, el servo, la sirena, el LED de estado y un lector simulado mientras llega el PN532. Cómo cargarlo y probarlo está en `firmware/esp32-main/README.md`. Tómalo como base: revisa el PR y cambia lo que quieras, sigue siendo tuyo.
- **`sim-esp32` listo (`tools/sim-esp32/`):** corre en el PC con los comandos de C11 (`tarjeta`, `celular`, `reiniciar`, `cortar-red`, `no-confirmar`) y reusa `controlador.py` y `api.py` del firmware, así que se comporta igual que la placa. Ya no hace falta la placa para el H2 ni para que Ana y Wili prueben.
- **`lector_pn532.py` escrito (25 de octubre):** I2C en GPIO 21 y 22, dirección `0x24`, misma interfaz que el simulado (`leer()` devuelve el UID en hex mayúsculas o `None`) y sin bloquear el latido. Tiene 17 pruebas en el PC contra un PN532 simulado y ya lee tarjetas con el módulo real (Elechouse V3). Se activa con `"lector": "pn532"` en `config.json`.
- **Reinicio del PN532 por pasos (25 de octubre, para que lo revises):** con la placa en WiFi contra la API, el lector se caía al acercar la tarjeta (los cables van a presión en el conector I2C) y cada reinicio detenía el bucle hasta 558 ms cada 2 s, porque `_iniciar()` esperaba cada respuesta con `sleep`; en una prueba la ESP32 pasó 10 s sin latidos y cerró por RF-25. Ahora el reinicio avanza en cada `leer()` sin esperar, igual que la búsqueda; solo el primer inicio, antes del bucle, espera. Hay 4 pruebas nuevas que fallan con la versión anterior. Lo que sigue siendo tuyo: soldar el conector de 4 pines del PN532.
- **Lo que falta y es tuyo:** probar el PN532 con tarjetas reales y pasar sus UID para agregarlos, conectar y calibrar el servo y la sirena (los pulsos de 0,5 y 1,5 ms son un punto de partida), y probarlo todo con la red de la demo. Si tienes código del PN532 o del servo, súbelo y lo juntamos.
- **`config.json` (contratos v1.8):** agrega `lector` (`"pn532"` o `"simulado"`) y `uidsPrueba`. Copia `config.example.json` como `config.json`; ese archivo no se sube porque lleva la clave del WiFi.

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

- **La API ahora usa PostgreSQL:** tus rutas no cambian. Si corres la API en tu laptop, necesitas Docker Desktop y los pasos de "Base de datos" en `server/README.md`; para probar con la laptop de Alex no hace falta nada.
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
  - `horaFotograma` es el instante en que la persona tocó la línea, interpolado entre dos fotos; no la foto que confirma el cruce al salir de la franja (Contratos de integración v1.10).
  - Un cruce de entrada consume el paso si su fotograma cae dentro de los 10 s de la ventana. Sin paso disponible, responde `fraude` con un `alertaId`.
- **Latido** `POST /api/torniquetes/tq_1/vision/latido` cada 5 s, con `fps` y `sesionVision`. Tras 15 s sin señal, el panel muestra la visión **fuera de línea**.
- **Credencial:** envía la cabecera `X-Device-Key` con la clave de la visión en los cruces y en los latidos; según `vision/config.yaml`, va en la variable de entorno `TG_VISION_KEY`. Es distinta de la del ESP32: la del ESP32 no sirve en las rutas de la visión, ni al revés. Mismo calendario que para Karol: tolerante hasta después del H1.
- **Cámara (C3):** el firmware ya está en `firmware/esp32-cam/` y transmite MJPEG en `http://<ip>:81/stream`, VGA 640×480 con calidad 12. Medido desde el PC: 24,6 FPS, ~12 KB por cuadro. `http://<ip>/` muestra el video para ajustar el encuadre y `http://<ip>/capture` da un JPEG suelto. Para leerlo usa `LectorStream` de `vision/stream.py`: `cv2.VideoCapture` se atasca con este stream (huecos de más de 1 s). La orientación (`VOLTEAR_VERTICAL`, `ESPEJO_HORIZONTAL`) se ajusta en `config.h` al montarla.
- **Servicio (C4):** ya está en `vision/` y cumple las reglas de arriba; ver `vision/README.md`. Lo que falta es calibrarlo con la CAM montada: la línea, la franja (`margen_linea`) y el sentido van en `config.yaml`, y `--ver` muestra todo encima del video.
- **RNF-06:** cada cruce deja en el log de la API una línea `RNF-06` con la hora del fotograma, la hora de llegada y el retraso en milisegundos.

### Ana — web (interfaz C6)

- **Proyecto listo en `web/`** (Vite + React + TypeScript). Para empezar: API arriba en el 5000, luego `npm install` y `npm run dev` en `web/`, y abrir `http://localhost:5173`. El proxy de Vite reenvía `/api`, `/hubs`, `/openapi` y `/scalar` a la API, así que no hace falta CORS. Los detalles están en `web/README.md`.
- **Tipos:** `src/api/esquema.d.ts` se genera con `npm run tipos` (con la API arriba) y está en el repositorio. `src/api/cliente.ts` exporta `api`, un cliente tipado: `api.GET('/api/torniquetes')`, `api.POST('/api/alertas/{id}/apagar-alarma', { params: { path: { id } } })`. Los enteros (`intentoId`, `pasoId`) salen como `number`.
- **La web ya cubre todo lo que tiene la API (16 de octubre):** las pantallas de la tabla de `web/README.md` están hechas y probadas con el guion completo, a 375 px. Las construí yo para llegar al H2; tú quedas como dueña: revisa el PR, cambia lo que quieras del diseño y toma la QA del MVP: RNF-31 en un celular real, RNF-33 en los navegadores, RNF-30 con 5 usuarios y RNF-32 (que cada mensaje se entienda).
- **Cómo está armada:** `useSesion()` para la sesión, `useEvento('Nombre', e => …)` para escuchar el hub (una sola conexión mientras hay sesión) y `src/textos.ts` para pasar los códigos de la API a español claro. Detalles en `web/README.md`.
- **Estilo nuevo (30 de octubre, para que lo revises):** paleta «índigo cálido» (fondo crema, índigo solo para acciones; verde, ámbar y rojo quedan para los estados), con contraste WCAG AA comprobado en claro y oscuro; iconos de `lucide-react` en el menú, los botones y los estados (en el celular, el icono va encima del texto para que quepan las cinco pestañas); y animaciones que muestran lo que pasa en vivo: el saldo cuenta hasta el valor nuevo, el brazo del torniquete gira con una barra de 10 s para la ventana del paso, la alerta de colado pulsa y hace vibrar el celular, y los intentos nuevos entran deslizándose. Todo respeta «reducir movimiento» del sistema. La lógica está en `src/movimiento.ts`; la ventana de 10 s está fija en `Panel.tsx` (la API no la envía).
- **Pendientes de la web:** filtros por fecha del historial (RF-40) y la clasificación de alertas (RF-35), que van después del MVP; las pantallas de administración (RF-41 a RF-44), cuando la API las tenga.
- **Sesión (Contrato §2):** `POST /api/auth/login { correo, password }` deja la cookie y responde `{ rol, nombre }`; `GET /api/auth/sesion` dice quién está al recargar (401 si nadie); `POST /api/auth/logout`. Sin sesión, toda ruta protegida responde 401 `SesionRequerida`, y con otro rol, 403 `SinPermiso`; la API nunca redirige. Tras 6 intentos fallidos responde 401 `LoginBloqueado` por 5 minutos. El correo y la contraseña del operador te los paso por privado (están en los secretos de usuario).
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
  - Cada solicitud nueva llega en vivo con el evento `SolicitudNueva { solicitudId, cliente: "Luisa R.", fecha }`, para agregarla a la lista sin recargar.

- **Portal del cliente (listo en la API, Contrato §3 a §5 y §9):**
  - `POST /api/clientes` registra e inicia sesión (RF-01). Los errores dicen qué campo falla: `NombreRequerido`, `TipoDocumentoInvalido`, `DocumentoInvalido`, `CorreoInvalido`, `TelefonoInvalido`, `PasswordCorta`, `AutorizacionRequerida`, `CorreoDuplicado` y `DocumentoDuplicado`.
  - `GET /api/tarjetas/mi-tarjeta` → `{ tarjetaId, saldo, soporte: { tipo: "fisico", estado } | null }`. Para el texto de RF-16 CA1: con `estado: "Entregada"` va el botón "Confirmar que la recibí" (`POST .../confirmar-recepcion`).
  - `POST .../solicitud-fisica` (409 `TarjetaFisicaExistente` si ya tiene una vigente).
  - `POST .../recargas { monto, simularRechazo }` con la cabecera `Idempotency-Key` (genera una con `crypto.randomUUID()` por cada intento de recarga, así un doble clic no cobra dos veces). Responde `{ recargaId, monto, aprobada, saldoNuevo, fecha }`. `simularRechazo: true` es para mostrar el pago rechazado (RF-15 CA2).
  - `GET .../recargas` y `GET .../viajes`: historiales, el más reciente primero. Un viaje rechazado trae `motivo` y no trae `tarifa` ni `saldoRestante`.
  - En vivo, por el hub y solo al dueño de la tarjeta: `SaldoActualizado { saldo, causa: "Paso|Recarga|Reversion", fecha }` y `EventoViaje { intentoId, resultado, motivo, torniquete, tarifa, fecha }` (tipos en `src/api/eventos.ts`).
  - El hub `/hubs/eventos` ahora pide sesión: el personal entra al grupo `operacion` y cada cliente al suyo. Conéctalo después de iniciar sesión.

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
- Estas rutas piden sesión del personal (Operador, Seguridad o Administrador); sin sesión responden 401 y con sesión de cliente, 403.
- El OpenAPI para generar los tipos TypeScript está en `http://<IP>:5000/openapi/v1.json`.

### Jeisy — revisión del módulo de cuentas (interfaz C7)

- Desde el 16 de octubre el módulo de cuentas, la base de datos e Identity los construyo yo (ver "Decisiones"). Te pido que revises esos PR: eres revisora de C7, y si ya tenías algo avanzado sin subir, avísame para no repetirlo.
- Lo que cambió en C7 (contratos de integración v1.6): nueva interfaz `ISolicitudesTarjeta` en `Application/Cuentas/` para la entrega en taquilla (`PendientePorIdAsync`, `PendientesAsync`, `LigarUidAsync`). Una solicitud es el `SoporteTarjeta` físico en `Solicitada`; su id es el del soporte.
- **Segundo PR (`feat/cuentas-tarjetas-saldo`), para revisar:**
  - Dominio en `Domain/Clientes/` y `Domain/Tarjetas/`: `Cliente`, `Tarjeta` (saldo; `Debitar` no deja saldo negativo, S3), `SoporteTarjeta` (Solicitada → Entregada → Activa, o Bloqueada con `MotivoBloqueo`), `Recarga` y `DebitoPaso` (para revertir un paso una sola vez, RF-46).
  - `ServicioCuentas` (en `Application/Cuentas/`) implementa C7 (`ISoporteResolver`, `ISaldoService`, `ISolicitudesTarjeta`) sobre los repositorios. Reemplaza a `CuentasDePrueba`, que ya no existe.
  - Casos de uso: `RegistrarCliente` (RF-01, RF-07), `SolicitarTarjetaFisica` (RF-08), `ConfirmarRecepcion` (RF-11), `ConsultarMiTarjeta` (RF-16), `Recargar` (RF-15), `ConsultarRecargas` (RF-17) y `ConsultarViajes` (RF-39).
  - La cuenta de acceso (correo y contraseña) queda detrás de `ICuentasAcceso`, que implementa Identity en el tercer PR. La contraseña nunca llega al dominio (RNF-17).
  - C7 v1.7: `SoporteResuelto` lleva `ClienteId`, para avisarle al cliente de su paso y su saldo.
  - Los cambios de saldo de una tarjeta se serializan con `CerrojoTarjetas` (RNF-16); hay una prueba de 20 recargas en paralelo.
  - La tarifa y los montos de recarga pasaron a la sección `Cuentas` de `appsettings.json`.
- Decisiones de C7 que siguen vigentes: `EstadoSoporte` y `TipoSoporte` están en `Domain/Tarjetas/`; `ResultadoDebito` es `(bool Exitoso, decimal SaldoRestante)` y si el saldo no alcanza no se descuenta nada (S3); el dinero es `decimal` adentro y entero en el JSON; el id del paso se reserva antes de debitar; el débito, el paso y el intento se guardan con un solo `IUnidadDeTrabajo.GuardarCambiosAsync` (RNF-15).
- **Tercer PR (`feat/postgres-identity`), para revisar:**
  - `TransitGate.Infrastructure`: `TransitGateDbContext` (Identity + todas las entidades salvo el torniquete), la migración `Inicial`, los repositorios y las secuencias de ids. Es también la `IUnidadDeTrabajo`: un solo `SaveChanges` por caso de uso (RNF-15). El saldo usa `xmin` como control de concurrencia (RNF-16).
  - Identity: `Usuario` (en un cliente, su Id es el del `Cliente`), roles `Cliente`, `Operador`, `Seguridad` y `Administrador`, contraseña de al menos 8 caracteres, bloqueo tras 6 intentos (RNF-22) y cookie de 60 min (RNF-18).
  - El registro abre una transacción que confirma el `GuardarCambiosAsync` final, porque Identity guarda por su cuenta: si algo falla, no queda una cuenta sin cliente.
  - `TransitGate.Api.Tests`: pruebas de integración con Testcontainers (401, 403, el guion del H2, idempotencia, 20 recargas en paralelo y una transacción que falla a la mitad).
- Para la app (C5): `POST /api/auth/login` ya funciona con cookie. El token de vida corta para Android (Contrato §2) y `soporte-virtual` llegan con la Fase 4.

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

- **RNF-03:** cada petición a `/api` registra cuánto tardó dentro del servidor. Los latidos no aparecen salvo que fallen o pasen de 200 ms, para no llenar el log (llegan cada 300 ms). Ya con la base de datos, falta la medición oficial (p95 ≤ 200 ms con 1.000 tarjetas y 10.000 intentos cargados).

- **RNF-04:** se compara `desde` con la hora en que el ESP32 aplica el cambio (en su propio log, con NTP). Cuando un paso vence, `desde` es la hora en que vencía el plazo, no la hora en que el servidor lo notó.
- **RNF-06:** `retrasoMs` cubre del servicio de visión a la API. El tramo de la cámara al servicio se mide con el cronómetro filmado.
- Para guardar el log de una sesión (PowerShell, desde `server/`):

```
dotnet run --project TransitGate.Api | Tee-Object -FilePath mediciones.log
```

## Próximos pasos

Lo que sigue, en orden (17 de octubre). Lo pendiente de cada integrante está en su sección de "Qué necesita saber cada uno".

1. **`sim-vision`** (`tools/sim-vision/`, C11): latido cada 5 s y comandos `entrada`, `salida`, `colado`, `retraso <ms>` y `silencio <s>`. Con él y `sim-esp32` se hace el guion completo sin hardware (RNF-38). Es de Wili; si no llega pronto, lo hace Alex con el mismo enfoque que `sim-esp32`.
2. **UID de las tarjetas reales:** el PN532 ya lee tarjetas (25 de octubre); falta pasarle a Alex sus UID para registrarlas y soldar los pines del módulo antes de la demo.
3. **Pines del PN532 soldados:** la ESP32 ya está en línea por WiFi contra la API (25 de octubre), pero el lector se cae al acercar la tarjeta porque los cables van metidos a presión en el conector I2C.
4. **Servo y sirena** conectados a la placa y calibrados (C8).
5. **Prueba del ángulo cenital** con la CAM montada y personas reales: 20 colados, 40 pasos y 50 cruces (RNF-07 a RNF-09). Es el riesgo técnico más grande (MVP §8). Si no llega a las metas, se ajusta en el orden de RNF §10.3 y solo al final se afina YOLO con imágenes propias.
6. **Servidor:** modo estricto de `X-Device-Key`, medición de RNF-03, registrar quién apagó la alarma y, en la semana 6, RF-46.

## Limitaciones actuales

| Limitación | Cuándo se resuelve |
| --- | --- |
| Al reiniciar la API se pierden el paso pendiente y la alarma sonando (el estado del torniquete vive en memoria; ver "Decisiones") | Así se queda: el torniquete arranca en `Cerrado` (RF-45 CA3) |
| `X-Device-Key` en modo tolerante | Se activa el modo estricto después del H1 (semana 3) |
| Falta la medición oficial de RNF-03 con 1.000 tarjetas y 10.000 intentos | Semana 3 |
| No se revierte el débito si el brazo no abre (RF-46) | Semana 6 |
| Quién apagó la alarma no queda registrado (HU-22 CA1 pide "con mi nombre"); la sesión ya existe | Semana 3 |
| El desafío del celular es fijo | Fase 4 |
| En la práctica hay un solo rol de personal: el Operador puede todo lo del panel (MVP §3.1), así que RF-06 CA3 (taquilla sin apagar alarmas) todavía no aplica | Entrega final |
| No hay recuperación de contraseña (RF-04) ni administración de operadores (RF-42) | Después del MVP (Should) |
| La visión todavía no se ha medido con personas en vista cenital (RNF-07 a RNF-09), y no hay `sim-vision` para el guion sin hardware | Prueba del ángulo con la maqueta; `sim-vision` en la semana 3 |
| Los pines del PN532 no están soldados: al acercar la tarjeta el lector se cae (`ENODEV`, `ETIMEDOUT`) y no siempre alcanza a leerla | Antes del H2 (Karol) |

## Historial

**30 de octubre (semana 3)**
- Web: paleta «índigo cálido» con contraste AA en los dos modos (el botón de alarma ya no lleva texto blanco sobre el rojo claro del modo oscuro), iconos con `lucide-react` y animaciones con propósito (saldo, torniquete y ventana del paso, alerta, intentos nuevos), con respaldo para que el saldo quede en el valor real aunque el navegador pause la animación.

**25 de octubre (semana 3)**
- Lector PN532 por I2C en el firmware (`lector_pn532.py`), sin bloquear el bucle principal y con reintento si el lector falla. Probado con el módulo real; 17 pruebas en el PC contra un PN532 simulado.
- Hallazgos con el módulo real: mientras procesa una orden no contesta a su dirección (`ENODEV`), a veces entrega la respuesta sin mostrar antes el ACK, y el jumper de 3,3 V de la placa de expansión tiene que estar puesto para que el módulo encienda.
- Firmware del ESP32-CAM en Arduino (`firmware/esp32-cam/camara/`), basado en CameraWebServer: página, `/capture` y MJPEG en `:81/stream` (C3), con FPS y RSSI en el puerto serie. Probado en la placa (OV3660, PSRAM de 4 MB) por WiFi de 2,4 GHz: 24,6 FPS en VGA con calidad 12.
- Servicio de visión en `vision/` (C4): YOLO nano preentrenado a 320 px con ByteTrack, conteo de cruces con una franja alrededor de la línea (el temblor del recuadro sobre la línea contaba entrada y salida a la vez) y un reporte por `trackId`. Cruces por una cola con plazo de 1 s y 3 reintentos, latido cada 5 s y retrasos en `cruces.csv` (RNF-06). 17 FPS procesados en el CPU. Probado contra la API en modo estricto: `consumido` con `sim-esp32`, `fraude`, `registrado`, `duplicado` y 401. 23 pruebas.
- Contratos de integración v1.9: `imgsz` y `margen_linea` en el `config.yaml` de la visión.
- Visión: el stream se lee con un lector propio (`vision/stream.py`) en vez de `cv2.VideoCapture`, que se atascaba con la ESP32 (huecos de hasta 1,2 s y caídas a 2 FPS que la cámara no tenía). En 60 s: de 8 esperas de más de 300 ms a ninguna, ~25 FPS procesados. La ventana de `--ver` ya no se congela sin señal y el modelo se calienta antes de conectar. 4 pruebas nuevas.
- Visión: `horaFotograma` es ahora el instante en que la persona tocó la línea, interpolado linealmente entre las dos fotos en que cambió de lado. Antes era la foto que confirma el cruce al salir de la franja, que llega unas fotos después. Contrato de la API v1.5 y Contratos de integración v1.10 (solo cambia la definición; el formato es el mismo). 5 pruebas nuevas.
- ESP32 principal en línea por WiFi contra la API en modo estricto: latidos de ~30 ms, el PN532 leyó una tarjeta real (`CB175353`) y la API respondió `TarjetaNoRegistrada`.
- Firmware: el reinicio del PN532 va por pasos y ya no detiene el bucle. Con los pines flojos el lector se caía al acercar la tarjeta y cada reinicio bloqueaba el bucle hasta 558 ms cada 2 s; en una prueba la ESP32 pasó 10 s sin latidos y cerró por RF-25. 4 pruebas nuevas.

**17 de octubre (semana 2)**
- Firmware del ESP32 principal en MicroPython (`firmware/esp32-main/`): reglas de C1 con pruebas, cliente HTTP con timeouts, WiFi, servo, sirena, LED de estado y lector simulado. Probado en la placa sin red y, desde el PC, contra la API.
- Contratos de integración v1.8: `lector` y `uidsPrueba` en el `config.json` del firmware.
- `sim-esp32` en `tools/sim-esp32/` (RNF-38), con los comandos de C11 y el mismo código del firmware. Probado contra la API: pasos, rechazos, vencimiento, corte de red, reinicio y `no-confirmar`.

**16 de octubre (semana 2), quinta parte**
- Web completa para el MVP con React Router: inicio de sesión, registro, mi tarjeta (saldo en vivo, pedir y confirmar la física), recargar, viajes, recargas, perfil, panel en vivo y taquilla con modo registro.
- La sesión y el hub se comparten en toda la web (`useSesion`, `useEvento`); si la sesión vence, vuelve a `/entrar`.

**16 de octubre (semana 2), cuarta parte**
- PostgreSQL con Docker (`docker-compose.yml`) y EF Core en `TransitGate.Infrastructure`, con la migración inicial; la API la aplica al arrancar y carga las semillas.
- Sesión con Identity (cookie): login, logout, `GET /api/auth/sesion`, roles y bloqueo tras 6 intentos. El panel, la taquilla y el hub piden sesión.
- Rutas del portal del cliente: registro, perfil, mi tarjeta, solicitud física, confirmación, recargas (con `Idempotency-Key`) e historiales.
- Pruebas de integración contra PostgreSQL (`TransitGate.Api.Tests`).
- La página de prueba de la web pide iniciar sesión. Contrato de la API v1.4.

**16 de octubre (semana 2), tercera parte**
- Módulo de cuentas real (en memoria): `Cliente`, `Tarjeta`, `SoporteTarjeta`, `Recarga` y `DebitoPaso`, con `ServicioCuentas` implementando C7. Reemplaza a `CuentasDePrueba`; los datos de prueba ahora son clientes de verdad.
- Casos de uso de registro, tarjeta física, recarga, saldo e historiales, con sus pruebas (RF-01, RF-07, RF-08, RF-11, RF-15, RF-16, RF-17, RF-39).
- Eventos `SolicitudNueva` (taquilla), `SaldoActualizado` y `EventoViaje` (cliente).
- Contratos de integración v1.7 (C7).

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
