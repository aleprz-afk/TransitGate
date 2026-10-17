# TransitGate — Contratos de integración (v1.8)

El [`TransitGate_Contrato_API.md`](./TransitGate_Contrato_API.md) define las rutas HTTP del servidor. Este documento define todo lo demás que dos integrantes distintos deben construir de forma compatible: el protocolo NFC entre la app y el lector, el stream de la cámara, los pines del hardware, los archivos de configuración, los eventos en tiempo real, el contrato interno entre los dos módulos del servidor y los simuladores. Si un integrante cambia algo de aquí sin avisar, se rompe el trabajo de otro.

**Cambios en v1.8 (17 oct 2026):** sección 9 — el `config.json` del firmware agrega `lector` (`"simulado"` o `"pn532"`) y `uidsPrueba`, las tarjetas que "acerca" el lector simulado mientras no está el PN532.

**Cambios en v1.7 (16 oct 2026):** C7 — `SoporteResuelto` agrega `ClienteId`, para avisarle al cliente de su paso y su saldo; `INotificador` agrega `SolicitudNuevaAsync` y `EventoViajeAsync`, y `SaldoActualizadoAsync` recibe la causa como enum (`CausaSaldo`). El contenido de los eventos de la sección 6 no cambia.

**Cambios en v1.6 (16 oct 2026):** C6 — evento `RegistroTarjeta` al grupo `operacion` con el resultado de cada lectura en modo registro (RF-10). C7 — Alex pasa a ser dueño del módulo de cuentas, tarjetas y saldo, con Jeisy como revisora; se agrega `ISolicitudesTarjeta` para entregar la tarjeta física en taquilla (RF-09, RF-10, RF-36) y `INotificador.RegistroTarjetaAsync`.

**Cambios en v1.5 (16 oct 2026):** C6 — `npm run build` deja la web directamente en `wwwroot` de `TransitGate.Api` (ya no hay que copiarla); el proxy de Vite también reenvía `/openapi` y `/scalar`, y la dirección de la API se puede cambiar con `API_URL`. Las rutas de la API que no existen siguen respondiendo 404 y no `index.html`.

**Cambios en v1.4 (14 oct 2026):** C7 — `INotificador` agrega `IntentoRegistradoAsync` y `AlertaActualizadaAsync`; `AlertaNuevaAsync` recibe la alerta en lugar de su id, por la misma razón que `EstadoTorniqueteAsync`. El contenido de los eventos de la sección 6 no cambia.

**Cambios en v1.3 (14 oct 2026):** los contratos pasan a vivir en `docs/` del repositorio. C6 — el evento `EstadoTorniquete` agrega `estadoDesde` y `ultimaSenal`, para que el panel muestre desde qué hora está en alarma y desde cuándo está fuera de línea (HU-20). C7 — se definen `EstadoSoporte` y `ResultadoDebito`, que el contrato usaba sin definir; `INotificador.EstadoTorniqueteAsync` recibe el estado ya calculado en lugar del id; se agrega `IUnidadDeTrabajo` para la transacción del paso (RNF-15); el id del paso se reserva antes de debitar.

**Cambios en v1.2 (9 oct 2026):** C6 — la web se construye en React (Vite) en lugar de Blazor WebAssembly. La API sigue sirviendo la web en el mismo puerto; se agrega el proxy de Vite para desarrollo.

---

## 1. Mapa de interfaces

| # | Interfaz | Lado A (dueño) | Lado B (dueño) | Formato | Se prueba con |
| --- | --- | --- | --- | --- | --- |
| C1 | ESP32 ↔ API | Firmware (Karol) | Backend validación (Alex) | HTTP/JSON, Contrato API §6 | `sim-esp32` y API esqueleto |
| C2 | App Android ↔ ESP32 | App (Jeisy) | Firmware (Karol) | APDU ISO 7816-4 por NFC | Lector USB o PN532 + app de prueba |
| C3 | ESP32-CAM → Visión | Cámara (Wili) | Visión (Wili) | MJPEG por HTTP | Navegador y OpenCV |
| C4 | Visión ↔ API | Visión (Wili) | Backend validación (Alex) | HTTP/JSON, Contrato API §7 | `sim-vision` y API esqueleto |
| C5 | App Android ↔ API | App (Jeisy) | Backend cuentas (Alex) | HTTP/JSON, Contrato API §2 y §4 | Scalar |
| C6 | Web ↔ API | Web (Ana) | Backend (Alex) | HTTP/JSON + SignalR | Scalar y datos semilla |
| C7 | Módulo de validación ↔ módulo de cuentas | Backend validación (Alex) | Backend cuentas (Alex; revisa Jeisy) | Interfaces C# | Pruebas unitarias |
| C8 | ESP32 ↔ PN532, servo y sirena | Firmware (Karol) | Hardware (Karol) | Pines e I2C | Banco de pruebas |

Los dueños son los integrantes del equipo según el plan de trabajo (v2.1). Los códigos C1–C8 identifican cada interfaz.

---

## 2. C1 — ESP32 principal ↔ API (comportamiento del cliente)

Las rutas y los JSON están en el Contrato API §6. Aquí va lo que el firmware debe hacer alrededor de ellos:

| Regla | Valor | Origen |
| --- | --- | --- |
| Periodo del latido | 300 ms | S11 |
| Timeout del latido | 250 ms; si vence, se cuenta como fallo y no bloquea la lectura del PN532 | RNF-01 (nota) |
| Timeout de una lectura | 3 s; si vence, no se abre el brazo | S10, RF-25 CA1 |
| Reintento de una lectura | Una vez, con el mismo `lecturaId` | RF-18 CA4 |
| Tarjeta quieta sobre el lector | El mismo UID dentro de 3 s no genera otra lectura | RF-18 CA3 |
| Sin comunicación | Si todos los latidos fallan durante 10 s, el ESP32 cierra el brazo y apaga la sirena por su cuenta | RF-25 CA2 |
| Tras reiniciar | Arranca con brazo cerrado y sirena apagada, genera un `arranque` nuevo y aplica lo que diga el primer latido | RF-45 CA3, RNF-11 |
| Confirmación de apertura | Al abrir para un `pasoId`, lo reporta en `pasoIdAbierto` en el siguiente latido | RF-22 CA4, S15 |
| Formato del UID | Hex en mayúsculas, longitud real (8 o 14 caracteres) | RNF-35 |
| `lecturaId` | `arranque` + `-` + contador de 4 dígitos (`a1f3-0007`) | RF-18 |
| Hora | Sincroniza por NTP solo para los registros de RNF-04; la lógica no depende del reloj del ESP32 | RNF-04 |

**Cómo distingue una tarjeta de un celular (RF-18 CA5):** si el SAK del objetivo tiene el bit `0x20` (ISO 14443-4), el ESP32 intenta el SELECT AID de la sección 3; si responde `90 00`, sigue el flujo del celular; si no, envía el UID como tarjeta física.

---

## 3. C2 — App Android ↔ ESP32 (APDU por NFC)

Prioridad Should (Fase 4). Se define ahora para que el firmware deje el hueco listo desde la Fase 1.

### 3.1 Identificador de la app (AID)

```
AID = F0 54 47 41 54 45 01   (F0 = propietario; "TGATE"; versión 01)
```

La app lo declara en `apduservice.xml` con `category="other"`. HCE funciona con la pantalla encendida.

### 3.2 Comandos

**SELECT** (el ESP32 lo envía primero):

```
→ 00 A4 04 00 07 F0544741544501 00
← <soporteId: 16 bytes, UUID> 90 00     app vinculada
← 69 85                                 app instalada pero sin vincular
← 6A 82 (o nada)                        no es la app: se trata como tarjeta física
```

**FIRMAR** (instrucción propia):

```
→ 80 10 00 00 Lc <L: 1 byte> <torniqueteId: L bytes UTF-8> <desafio: 16 bytes> 00
← <firma: 32 bytes> 90 00
← 6A 80                                 datos mal formados
← 69 85                                 sin vincular
```

### 3.3 Cálculo de la firma

```
mensaje = "TG1" || torniqueteId (UTF-8) || 0x00 || desafio (16 bytes)
firma   = HMAC-SHA256(claveSecreta (32 bytes), mensaje)
```

- `desafio` es el `desafio.valor` del último latido, convertido de hex a bytes.
- Incluir el `torniqueteId` hace que una firma calculada en T1 no sirva en T2 (RF-19 CA4).
- El ESP32 envía al servidor `soporteId` como UUID con guiones y `firma` en hex mayúsculas (64 caracteres).
- La app guarda la clave en Android Keystore y nunca la escribe en logs (RNF-21).

### 3.4 Vector de prueba

Ambos lados (app y servidor) deben producir la misma firma para este caso antes de probar con hardware:

```
claveSecreta = 32 bytes 0x01 0x02 ... 0x20
torniqueteId = "tq_1"
desafio      = 16 bytes 0x00
```

El primero que lo implemente (servidor o app) publica la firma resultante en este documento y el otro la reproduce.

---

## 4. C3 — ESP32-CAM → servicio de visión

| Parámetro | Valor inicial | Nota |
| --- | --- | --- |
| Firmware | Ejemplo CameraWebServer de Arduino, sensor OV3660 | Modelo AI-Thinker |
| URL del stream | `http://<ip-cam>:81/stream` | Puerto por defecto del ejemplo |
| Resolución | VGA 640×480, calidad JPEG 12 | Se fija en la Fase 1 con la prueba de ángulo |
| FPS | ≥ 15 | RNF-37 |
| IP | Fija (reserva DHCP o estática en el código) | La visión la necesita en su config |
| Alimentación | Fuente de 5 V ≥ 2 A, no desde el USB del adaptador | RNF-37 |
| Montaje | Vista cenital sobre el torniquete | Arquitectura §7 |

El stream no se graba: el servicio de visión procesa en memoria y descarta cada fotograma (RNF-26).

### 4.1 Configuración del servicio de visión (`vision/config.yaml`)

```yaml
api_url: "http://192.168.137.10:5000"
torniquete_id: "tq_1"
device_key_env: "TG_VISION_KEY"     # la clave va en variable de entorno, no aquí (RNF-25)
stream_url: "http://192.168.137.21:81/stream"
modelo: "yolov8n.pt"                # preentrenado, clase person
confianza_min: 0.5
linea:                              # coordenadas normalizadas 0..1 sobre el fotograma
  p1: [0.0, 0.5]
  p2: [1.0, 0.5]
sentido_entrada: "arriba_a_abajo"
latido_seg: 5
```

---

## 5. C4 — Servicio de visión ↔ API (comportamiento del cliente)

Las rutas están en el Contrato API §7.

| Regla | Valor |
| --- | --- |
| Hora del cruce | `horaFotograma` = hora en que el fotograma llegó al servicio de visión, en el reloj de la laptop (mismo reloj que la API) (S13) |
| Idempotencia | `sesionVision` + `trackId`; un mismo ID que cruza dos veces se reporta una vez (RF-26 CA3) |
| Reintento | Hasta 3 veces con el mismo cuerpo si la API no responde en 1 s |
| Latido | Cada 5 s; la API la marca fuera de línea tras 15 s (S14) |
| Qué nunca se envía | Imágenes, recortes ni coordenadas de personas (RNF-26) |
| Medición de la gracia | El servicio registra en un CSV el retraso de cada cruce de prueba para calcular la gracia de S13 (RNF-06) |

---

## 6. C6 — Web ↔ API: tiempo real (SignalR)

**Decisión de integración:** la web (portal del cliente y panel de operación) se construye en **React con Vite**. Para la demo, `npm run build` deja la web compilada en `wwwroot` de `TransitGate.Api` (no se sube al repositorio) y la API la sirve en el mismo puerto (`http://<laptop>:5000`) con `UseStaticFiles()` y `MapFallbackToFile("index.html")`. Así no hay CORS y la cookie de sesión funciona igual en el portal y en el hub.

**En desarrollo:** el servidor de Vite corre en `http://localhost:5173` con proxy hacia la API, para que el navegador vea un solo origen y no haga falta abrir CORS:

```js
// web/vite.config.ts
const api = process.env.API_URL ?? "http://localhost:5000"
server: {
  proxy: {
    "/api": api,
    "/hubs": { target: api, ws: true },
    "/openapi": api,
    "/scalar": api
  }
},
build: { outDir: "../server/TransitGate.Api/wwwroot", emptyOutDir: true }
```

**Reglas para la web:**

- El cliente de SignalR es el paquete `@microsoft/signalr`.
- Sin sesión, la API responde 401 (no redirige a una página de login); la web es la que lleva al usuario a la pantalla de inicio de sesión. El servidor configura los eventos `OnRedirectToLogin` y `OnRedirectToAccessDenied` de la cookie para devolver 401 y 403 (RNF-19).
- Los tipos TypeScript de los JSON se generan desde el OpenAPI de la API (por ejemplo con `openapi-typescript`), no se escriben a mano (RNF-41).
- Las fechas llegan en UTC y se muestran en hora de Colombia con `Intl.DateTimeFormat` y `timeZone: "America/Bogota"` (RNF-42).

**Hub:** `/hubs/eventos`, autenticado con la cookie. Al conectar, el servidor mete la conexión en grupos según el rol:

| Grupo | Quién entra |
| --- | --- |
| `cliente:{clienteId}` | El cliente dueño de la sesión |
| `operacion` | Operador, Seguridad y Administrador |

Eventos y su contenido (los nombres de propiedad son los del JSON):

```jsonc
// grupo cliente:{id}
SaldoActualizado  { "saldo": 5600, "causa": "Paso|Recarga|Reversion", "fecha": "…" }
EventoViaje       { "intentoId": 77, "resultado": "Exitoso|Rechazado", "motivo": null,
                    "torniquete": "Torniquete 1", "tarifa": 3200, "fecha": "…" }

// grupo operacion
EstadoTorniquete  { "torniqueteId": "tq_1", "estado": "Cerrado|EsperandoCruce|Alarma|ModoRegistro",
                    "estadoDesde": "…",          // desde cuándo está en ese estado; null si no ha cambiado
                    "esp32Conectado": true,
                    "ultimaSenal": "…",          // último latido del ESP32; null si no ha llegado ninguno
                    "visionConectada": true, "desincronizado": false }
IntentoRegistrado { "intentoId": 77, "torniqueteId": "tq_1", "resultado": "Rechazado",
                    "motivo": "SaldoInsuficiente", "fecha": "…" }
AlertaNueva       { "alertaId": "al_9", "torniqueteId": "tq_1", "tipo": "FraudeSinValidacion", "fecha": "…" }
AlertaActualizada { "alertaId": "al_9", "estado": "Atendida|Resuelta" }
RegistroTarjeta   { "torniqueteId": "tq_1", "solicitudId": "s_44…", "fecha": "…",
                    "resultado": "Ligado|UidYaRegistrado|SolicitudNoPendiente" }   // lectura en modo registro (RF-10)
SolicitudNueva    { "solicitudId": "s_44", "cliente": "Luisa R.", "fecha": "…" }
```

El panel nunca muestra el documento completo ni el UID completo del cliente (RNF-27): `cliente` va abreviado y los UID se muestran con los últimos 4 caracteres.

---

## 7. C7 — Contrato interno del servidor

El servidor tiene dos módulos: validación (torniquete, colados) y cuentas (cuentas, tarjetas, saldo). Desde la v1.6 los dos los construye Alex, con Jeisy como revisora de cuentas; la separación se mantiene: el módulo de validación solo usa estas interfaces de `TransitGate.Application` para hablar con el de cuentas. Mientras no existan las implementaciones reales, la API usa implementaciones falsas en memoria.

```csharp
public enum EstadoSoporte { Solicitada, Entregada, Activa, Bloqueada }   // Domain/Tarjetas; el virtual usa Activa y Bloqueada

public record SoporteResuelto(Guid SoporteId, Guid TarjetaId, Guid ClienteId, EstadoSoporte Estado);

// Si el saldo no alcanza, Exitoso es false y no se descuenta nada (S3).
public record ResultadoDebito(bool Exitoso, decimal SaldoRestante);

public interface ISoporteResolver                    // módulo de cuentas
{
    Task<SoporteResuelto?> PorUidAsync(string uidHex);
    Task<SoporteResuelto?> PorSoporteIdAsync(Guid soporteId);
    Task<bool> VerificarFirmaAsync(Guid soporteId, string torniqueteId,
                                   byte[] desafio, byte[] firma);   // Fase 4
}

public interface ISaldoService                       // módulo de cuentas
{
    Task<decimal> TarifaVigenteAsync();
    Task<ResultadoDebito> DebitarAsync(Guid tarjetaId, decimal monto, long pasoId);
    Task RevertirAsync(long pasoId);                 // RF-46
}

public record SolicitudPendiente(Guid SoporteId, Guid ClienteId, string NombreCompleto, string Documento, DateTime Fecha);
public enum ResultadoLigar { Ligado, UidYaRegistrado, SolicitudNoPendiente }

public interface ISolicitudesTarjeta                 // entrega en taquilla (RF-09, RF-10, RF-36)
{
    Task<SolicitudPendiente?> PendientePorIdAsync(Guid soporteId);
    Task<IReadOnlyList<SolicitudPendiente>> PendientesAsync(string? documento);   // la más antigua primero
    Task<ResultadoLigar> LigarUidAsync(Guid soporteId, string uidHex);            // Solicitada → Entregada
}

public interface INotificador                        // implementa Alex (sobre SignalR)
{
    Task EstadoTorniqueteAsync(EstadoTorniqueteVista estado);
    Task IntentoRegistradoAsync(IntentoAcceso intento);
    Task AlertaNuevaAsync(AlertaSeguridad alerta);
    Task AlertaActualizadaAsync(AlertaSeguridad alerta);
    Task RegistroTarjetaAsync(string torniqueteId, Guid solicitudId, ResultadoLigar resultado);
    Task SolicitudNuevaAsync(SolicitudPendiente solicitud);
    Task SaldoActualizadoAsync(Guid clienteId, decimal saldo, CausaSaldo causa);   // Paso, Recarga, Reversion
    Task EventoViajeAsync(Guid clienteId, IntentoAcceso intento, string torniqueteNombre);
}

// Contenido del evento EstadoTorniquete (sección 6).
public record EstadoTorniqueteVista(string TorniqueteId, EstadoTorniquete Estado, DateTime? EstadoDesde,
    bool Esp32Conectado, DateTime? UltimaSenal, bool VisionConectada, bool Desincronizado);

public interface IUnidadDeTrabajo                    // Infrastructure (sobre el DbContext)
{
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
```

**Reglas:**

- `DebitarAsync` no abre su propia transacción: participa en la del caso de uso de validación, que guarda en un solo `IUnidadDeTrabajo.GuardarCambiosAsync` (un `SaveChanges`) el débito, el `IntentoAcceso` y el `PasoAutorizado` (RNF-15). Para eso, `ISaldoService` y los repositorios de Alex comparten el mismo `DbContext`. El débito usa control de concurrencia sobre el saldo (RNF-16).
- El id del paso se reserva **antes** de debitar, porque `DebitarAsync` lo recibe. Los pasos y los intentos toman su id de una secuencia de PostgreSQL (`UseHiLo` de Npgsql).
- Los métodos de `INotificador` reciben lo ya calculado (la vista o la entidad), no un id: se llaman con el cerrojo del torniquete tomado o justo después, y con solo el id el notificador tendría que volver a buscarlo; en el caso de `EstadoTorniqueteAsync`, la API se bloquearía.
- **Dueños de entidades:** Alex es dueño de todas: `Torniquete`, `PasoAutorizado`, `IntentoAcceso`, `Cruce` y `AlertaSeguridad` (validación) y `Cliente`, `Tarjeta`, `SoporteTarjeta`, `Recarga`, `Tarifa` y `Operador` (cuentas, revisa Jeisy). Las migraciones se fusionan en orden, nunca dos a la vez sobre la misma rama.

---

## 8. C8 — Pines del ESP32 principal (propuesta)

| Señal | Pin del ESP32 | Conexión | Nota |
| --- | --- | --- | --- |
| PN532 SDA | GPIO 21 | I2C, dirección `0x24` | Interruptores del módulo en modo I2C |
| PN532 SCL | GPIO 22 | I2C | |
| Servo (actuador 1) | GPIO 13 | PWM 50 Hz | Alimentación de 5 V aparte, tierra común |
| Sirena (actuador 2) | GPIO 26 | Transistor o módulo relé | El ESP32 trabaja a 3,3 V: nunca directo |
| LED de estado | GPIO 2 | LED de la placa | Parpadeo = sin conexión; fijo = modo registro |

Karol puede cambiar los pines, pero debe actualizar esta tabla y el `config.json` en el mismo PR.

---

## 9. Archivos de configuración

Los que tienen secretos (contraseña del WiFi, claves de dispositivo) no se suben al repositorio; se sube un `*.example` con valores falsos (RNF-25).

**Firmware** — `firmware/esp32-main/config.json` (en el repo: `config.example.json`):

```json
{
  "wifi": { "ssid": "POR_DEFINIR", "password": "POR_DEFINIR" },
  "servidor": "http://192.168.137.10:5000",
  "torniqueteId": "tq_1",
  "deviceKey": "POR_DEFINIR",
  "latidoMs": 300,
  "timeoutLatidoMs": 250,
  "timeoutLecturaMs": 3000,
  "cierreSinConexionMs": 10000,
  "pines": { "sda": 21, "scl": 22, "servo": 13, "sirena": 26, "led": 2 },
  "lector": "pn532",
  "uidsPrueba": ["04A1B2C3", "DEADBEEF"]
}
```

`lector` es `"pn532"` con el lector conectado o `"simulado"` para probar sin él: el botón BOOT "acerca" la siguiente tarjeta de `uidsPrueba`, o se escribe un UID por el puerto serie.

**Servidor** — `appsettings.json`: escucha en `http://0.0.0.0:5000`; los valores por defecto de S4, S6, S8, S12 y S14 van aquí y lo que el administrador guarde en la base de datos prevalece (RNF-40). Las claves de dispositivo y la cadena de conexión van en `dotnet user-secrets`.

---

## 10. Red

| Uso | Red | Notas |
| --- | --- | --- |
| Desarrollo | Red de la PC del integrante (datos al iniciar la programación) | Cada integrante usa su propio `config.json` |
| Demo | Hotspot de la laptop de un compañero | Forzar la banda de 2,4 GHz y WPA2-Personal (RNF-36) |

Direcciones propuestas para la demo (si el hotspot es el de Windows, la red es `192.168.137.0/24`):

| Equipo | IP |
| --- | --- |
| Laptop servidor | `192.168.137.10` (estática) |
| ESP32 principal | DHCP (no hace falta fijarla) |
| ESP32-CAM | `192.168.137.21` (estática) |

En la laptop servidor: perfil de red Privada y regla de entrada en el firewall de Windows para el puerto 5000.

Todo va por HTTP dentro de esta red (arquitectura v2, §9). **Pendiente:** el RNF-20 todavía exige HTTPS para la web y la app; hay que actualizarlo para reflejar esta decisión y dejarlo como limitación conocida.

---

## 11. Simuladores y dobles de prueba (RNF-38)

Cada dueño de un dispositivo entrega su simulador, para que el otro lado no dependa del hardware. Ambos usan exactamente el Contrato API.

| Herramienta | Dueño | Carpeta | Qué hace |
| --- | --- | --- | --- |
| `sim-esp32` | Karol | `tools/sim-esp32/` (Python) | Latido cada 300 ms; comandos `tarjeta <uid>`, `celular <soporteId> <claveHex>`, `reiniciar`, `cortar-red <s>`, `no-confirmar` (para probar RF-46) |
| `sim-vision` | Wili | `tools/sim-vision/` (Python) | Latido cada 5 s; comandos `entrada`, `salida`, `colado` (dos entradas seguidas), `retraso <ms>`, `silencio <s>` |
| API esqueleto | Alex | `server/` | En la semana 1 responde con datos fijos a lecturas, latido y cruces, para que firmware y visión prueben contra algo real desde el inicio |

**Criterio de RNF-38:** con solo los dos simuladores y la API se ejecuta completo el flujo de pasar, colarse y apagar la alarma.

---

## 12. Cómo se cambia un contrato

1. Los contratos viven en `docs/` del monorepo.
2. Todo cambio va en un PR con la etiqueta `contrato`, aprobado por el dueño de cada lado de la interfaz afectada (tabla de la sección 1).
3. El PR sube la versión del documento y agrega una línea en su lista de cambios.
4. Si el cambio rompe a alguien, el mismo PR actualiza el simulador correspondiente.
