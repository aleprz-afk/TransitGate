# TransitGate — Requisitos funcionales (v2)

Sep 25, 2026 · @alex

## 1. Alcance y convenciones

Este documento especifica 46 requisitos funcionales (RF) de TransitGate en 8 módulos: 33 Must, 10 Should y 3 Could. Cada RF dice qué hace el sistema; el cómo queda para la especificación técnica, los RNF y el contrato de la API.

Cada RF sigue la plantilla de la guía: módulo, actor, prioridad, descripción, precondiciones, criterios de aceptación (Dado / Cuando / Entonces) y relaciones. Los valores numéricos de los criterios salen de los supuestos de la sección 2.

### Prioridades (MoSCoW)

| Prioridad | Significado en este proyecto |
| --- | --- |
| Must | Sin él la demo no cumple el objetivo ni los requisitos del profesor |
| Should | Importante, pero la demo funciona sin él |
| Could | Deseable si sobra tiempo |
| Won't | Fuera de esta versión (sección 11) |

### Actores

| Actor | Tipo | Canal |
| --- | --- | --- |
| Cliente | Persona | Portal web, app Android, torniquete |
| Operador de taquilla | Persona | Panel de operación |
| Personal de seguridad | Persona | Panel de operación |
| Administrador | Persona | Panel de operación |
| ESP32 principal | Sistema | API REST |
| Servicio de visión | Sistema | API REST |

### Resultados y motivos de un IntentoAcceso

Todo intento queda con un resultado (`Exitoso`, `Rechazado` o `Fraude`) y, si no es exitoso, un motivo del catálogo:

| Motivo | Cuándo se usa |
| --- | --- |
| `TarjetaNoRegistrada` | El UID o la clave no corresponde a ningún soporte |
| `SoporteNoActivo` | Tarjeta física en estado `Solicitada` o `Entregada` |
| `SoporteBloqueado` | Soporte bloqueado por pérdida, reporte o reemplazo |
| `SaldoInsuficiente` | Saldo menor que la tarifa vigente |
| `FirmaInvalida` | La respuesta del celular no corresponde al desafío |
| `DesafioInvalido` | El desafío no lo emitió el servidor, ya se usó o expiró (S12) |
| `TorniqueteOcupado` | Hay un paso autorizado sin consumir |
| `TorniqueteEnAlarma` | El torniquete está en estado `Alarma` |
| `FraudeSinValidacion` | Cruce sin paso autorizado (resultado `Fraude`) |

### Motivos de bloqueo de un soporte

Un soporte bloqueado guarda uno de estos motivos; en el diagrama de clases es un enum.

| Motivo | Cuándo se usa |
| --- | --- |
| `Perdida` | El cliente reporta que perdió la tarjeta o el celular (RF-14) |
| `Reporte` | Robo o uso no autorizado, reportado por el cliente o por un operador (RF-14) |
| `Reemplazo` | Se activó otro soporte de la misma tarjeta (RF-13) |

### Estados usados en los criterios

- **Torniquete:** `Cerrado`, `EsperandoCruce`, `Alarma` y `ModoRegistro` (este último es nuevo, ver S6). La conexión no es un estado: se lleva aparte, como hora de la última señal de vida.
- **Paso autorizado:** `Pendiente` → `Consumido`, `Expirado` o `Revertido` (entidad `PasoAutorizado`).
- **Soporte físico:** `Solicitada` → `Entregada` → `Activa` → `Bloqueada`.
- **Soporte virtual:** `Activo` → `Bloqueado`.
- **Alerta de seguridad:** `Abierta` → `Atendida` → `Resuelta`.

## 2. Supuestos para revisar

Estos 15 supuestos cierran las decisiones pendientes de la guía para poder escribir criterios con valores concretos. Si cambian uno, solo hay que ajustar los RF de la columna "Afecta".

| ID | Decisión pendiente | Supuesto adoptado | Afecta |
| --- | --- | --- | --- |
| S1 | Soportes activos por cliente | Uno solo. Activar un soporte nuevo bloquea el anterior (motivo `Reemplazo`), previa confirmación del cliente | RF-12, RF-13 |
| S2 | Caída del WiFi o del servidor | Torniquete cerrado (modo seguro); no se debita nada. El prototipo no tiene liberación de emergencia (sección 11) | RF-25 |
| S3 | Tarifa y saldo negativo | Tarifa fija por paso, valor de ejemplo 3.200 COP; no se permite saldo negativo | RF-20, RF-21, RF-41 |
| S4 | Expiración del paso autorizado | 10 s sin cruce. Si el brazo abrió, la tarifa no se reembolsa; si no abrió, aplica S15 | RF-29, RF-46 |
| S5 | Paso de `Entregada` a `Activa` | El cliente confirma en el portal que recibió la tarjeta | RF-10, RF-11 |
| S6 | Modo registro del lector | El servidor lo maneja como estado `ModoRegistro` del torniquete, con duración máxima de 60 s | RF-09 |
| S7 | Sentido del cruce | Solo cuentan los cruces en sentido de entrada | RF-26 |
| S8 | Montos de recarga | Mínimo 1.000 y máximo 200.000 COP por recarga | RF-15 |
| S9 | Recuperación de contraseña | Enlace válido 30 min; en la demo el envío del correo puede simularse en el log del servidor | RF-04 |
| S10 | Tiempo de espera del ESP32 | Sin respuesta del servidor a una lectura en 3 s = sin conexión | RF-25 |
| S11 | Comunicación servidor → ESP32 | El ESP32 consulta cada 300 ms: recibe el estado deseado del torniquete (brazo, sirena) y reporta su estado real, que sirve de confirmación | RF-45, RF-46 |
| S12 | Desafío del celular | Lo genera el servidor y viaja en la consulta; es de un solo uso y expira a los 30 s | RF-19 |
| S13 | Gracia de visión | Los cruces se juzgan por la hora de llegada del fotograma al servicio de visión. La gracia es el máximo de al menos 100 mediciones del retraso completo (captura hasta API) más un margen; mientras se mide, vale 1 s | RF-26, RF-27, RF-29 |
| S14 | Plazos de señal de vida | Fuera de línea tras 5 s sin consultas del ESP32 o 15 s sin señal de la visión | RF-30, RF-37 |
| S15 | Confirmación de apertura | Si el ESP32 no reporta el brazo abierto para un paso dentro de 2 s, el débito se revierte | RF-46 |

## 3. Módulo 1 — Gestión de clientes y autenticación

Seis RF (RF-01 a RF-06) cubren la cuenta del cliente y el control de quién puede hacer qué. El registro dispara la creación de la tarjeta (RF-07).

### RF-01 — Registrar cliente

**Módulo:** Clientes y autenticación · **Actor:** Cliente · **Prioridad:** Must

**Descripción:** El sistema debe permitir que una persona cree una cuenta de cliente con nombre completo, tipo y número de documento, correo, teléfono y contraseña.

**Precondiciones:** La persona no tiene cuenta.

**Criterios de aceptación:**

- **CA1.** Dado un formulario con todos los datos válidos, cuando la persona envía el registro, entonces se crea el `Cliente`, se crea su `Tarjeta` con saldo 0 (RF-07) y la persona queda con sesión iniciada.
- **CA2.** Dado un correo o un documento que ya pertenece a otro cliente, cuando se envía el registro, entonces no se crea la cuenta y se indica el dato repetido.
- **CA3.** Dado un campo obligatorio vacío o con formato inválido (correo sin @, documento con letras), cuando se envía el registro, entonces no se crea la cuenta y se marca el campo con error.
- **CA4.** Dada una contraseña de menos de 8 caracteres, cuando se envía el registro, entonces no se crea la cuenta.
- **CA5.** Dada la casilla de autorización del tratamiento de datos sin marcar, cuando se envía el registro, entonces no se crea la cuenta (RNF-28).

**Relacionado con:** RF-07 · `Cliente`, `Tarjeta`

### RF-02 — Iniciar sesión

**Módulo:** Clientes y autenticación · **Actor:** Cliente, Operador, Personal de seguridad, Administrador · **Prioridad:** Must

**Descripción:** El sistema debe autenticar a un usuario con correo y contraseña y llevarlo a la vista de su rol.

**Precondiciones:** El usuario tiene una cuenta activa.

**Criterios de aceptación:**

- **CA1.** Dado un cliente con credenciales correctas, cuando inicia sesión en la web, entonces entra al portal del cliente y ve su saldo.
- **CA2.** Dado un operador o personal de seguridad con credenciales correctas, cuando inicia sesión, entonces entra al panel de operación.
- **CA3.** Dada una contraseña incorrecta, cuando intenta iniciar sesión, entonces no entra y el mensaje no revela si falló el correo o la contraseña.
- **CA4.** Dado un operador desactivado (RF-42), cuando intenta iniciar sesión, entonces no entra.
- **CA5.** Dado un cliente registrado, cuando inicia sesión en la app Android con las mismas credenciales de la web, entonces queda autenticado en la app.

**Relacionado con:** RF-06, RF-12, RF-42 · `Cliente`, `Operador`

### RF-03 — Cerrar sesión

**Módulo:** Clientes y autenticación · **Actor:** Cualquier usuario autenticado · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el usuario cierre su sesión.

**Precondiciones:** El usuario tiene una sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un usuario con sesión iniciada, cuando cierra sesión, entonces vuelve a la pantalla de inicio y cualquier página protegida le pide iniciar sesión otra vez.

**Relacionado con:** RF-02

### RF-04 — Recuperar contraseña

**Módulo:** Clientes y autenticación · **Actor:** Cliente · **Prioridad:** Should

**Descripción:** El sistema debe permitir que un cliente que olvidó su contraseña defina una nueva mediante un enlace enviado a su correo.

**Precondiciones:** El cliente tiene una cuenta.

**Criterios de aceptación:**

- **CA1.** Dado un correo registrado, cuando el cliente pide recuperar la contraseña, entonces se genera un enlace de un solo uso válido por 30 min (S9).
- **CA2.** Dado un correo no registrado, cuando se pide recuperar la contraseña, entonces se muestra el mismo mensaje que en CA1 y no se envía nada.
- **CA3.** Dado un enlace vencido o ya usado, cuando el cliente intenta usarlo, entonces se rechaza y debe pedir uno nuevo.
- **CA4.** Dado un enlace válido, cuando el cliente define la nueva contraseña, entonces puede iniciar sesión con ella y ya no con la anterior.

**Relacionado con:** RF-02 · `Cliente`

### RF-05 — Editar datos personales

**Módulo:** Clientes y autenticación · **Actor:** Cliente · **Prioridad:** Should

**Descripción:** El sistema debe permitir que el cliente modifique su nombre, correo, teléfono y contraseña.

**Precondiciones:** El cliente tiene una sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un cliente con sesión, cuando cambia su teléfono y guarda, entonces el nuevo valor aparece en su perfil.
- **CA2.** Dado un correo nuevo que ya usa otro cliente, cuando guarda, entonces no se aplica el cambio y se indica el motivo.
- **CA3.** Dado cualquier cliente, cuando intenta cambiar su número de documento, entonces el sistema no lo permite, porque el documento lo identifica en taquilla.
- **CA4.** Dado un cambio de contraseña, cuando guarda, entonces el sistema exige la contraseña actual.

**Relacionado con:** RF-01, RF-10 · `Cliente`

### RF-06 — Restringir el acceso por rol y por propietario

**Módulo:** Clientes y autenticación · **Actor:** Sistema (aplica a todos los usuarios) · **Prioridad:** Must

**Descripción:** El sistema debe permitir a cada usuario solo las acciones de su rol y, al cliente, solo sobre su propia tarjeta.

**Precondiciones:** Ninguna.

| Acción | Cliente | Operador de taquilla | Seguridad | Administrador |
| --- | --- | --- | --- | --- |
| Ver y recargar su propia tarjeta | Sí | No | No | No |
| Entregar tarjetas físicas (modo registro) | No | Sí | No | Sí |
| Ver estado, intentos y alertas | No | Sí | Sí | Sí |
| Apagar alarma y resolver alertas | No | No | Sí | Sí |
| Tarifa, operadores y torniquetes | No | No | No | Sí |

**Criterios de aceptación:**

- **CA1.** Dado el cliente A con sesión, cuando pide el saldo o el historial de la tarjeta del cliente B, entonces la API responde 404, sin revelar que el recurso existe.
- **CA2.** Dado un cliente con sesión, cuando intenta abrir el panel de operación, entonces se le niega el acceso.
- **CA3.** Dado un operador de taquilla, cuando intenta apagar una alarma o cambiar la tarifa, entonces la API responde 403.
- **CA4.** Dada una petición sin sesión, cuando pide cualquier dato de un cliente, entonces la API responde 401.

**Relacionado con:** todos los RF con actor humano · `Cliente`, `Operador`

## 4. Módulo 2 — Gestión de tarjetas

Ocho RF (RF-07 a RF-14) cubren el ciclo de vida de la tarjeta lógica y sus soportes. La regla central: la tarjeta guarda el saldo y los soportes solo la identifican, así que bloquear o reemplazar un soporte nunca toca el saldo.

### RF-07 — Crear la tarjeta lógica al registrarse

**Módulo:** Gestión de tarjetas · **Actor:** Sistema (al completar RF-01) · **Prioridad:** Must

**Descripción:** El sistema debe crear automáticamente una tarjeta lógica con saldo cero para cada cliente nuevo, y garantizar que ningún cliente tenga más de una tarjeta ni exista una tarjeta sin cliente.

**Precondiciones:** Registro exitoso (RF-01).

**Criterios de aceptación:**

- **CA1.** Dado un registro exitoso, cuando termina el RF-01, entonces existe exactamente una `Tarjeta` ligada al cliente, con saldo 0 y sin soportes.
- **CA2.** Dado un cliente que ya tiene tarjeta, cuando se intenta crearle otra por cualquier vía, entonces el sistema lo rechaza.
- **CA3.** Dada la base de datos en cualquier momento, cuando se buscan tarjetas sin cliente, entonces el resultado es vacío.

**Relacionado con:** RF-01, RF-08, RF-12 · `Tarjeta`, `Cliente`

### RF-08 — Solicitar tarjeta física

**Módulo:** Gestión de tarjetas · **Actor:** Cliente · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el cliente solicite desde el portal una tarjeta física para recogerla en taquilla.

**Precondiciones:** Sesión iniciada; el cliente no tiene un soporte físico en estado `Solicitada`, `Entregada` o `Activa`.

**Criterios de aceptación:**

- **CA1.** Dado un cliente sin soporte físico, cuando solicita la tarjeta, entonces se crea un `SoporteTarjeta` físico en estado `Solicitada`, sin UID, y aparece en la lista de solicitudes del panel (RF-36).
- **CA2.** Dado un cliente con un soporte físico `Solicitada`, `Entregada` o `Activa`, cuando intenta solicitar otra, entonces el sistema no lo permite.
- **CA3.** Dado un cliente cuyo soporte físico está `Bloqueada`, cuando solicita otra, entonces se crea una nueva solicitud (reposición) y el saldo no cambia.
- **CA4.** Dado un cliente con celular `Activo`, cuando solicita una tarjeta física, entonces la solicitud se crea y el celular sigue funcionando hasta que la física se active (RF-13).

**Relacionado con:** RF-10, RF-13, RF-36 · `SoporteTarjeta`

### RF-09 — Activar el modo registro en el lector

**Módulo:** Gestión de tarjetas · **Actor:** Operador de taquilla · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el operador ponga un torniquete en modo registro, para que la siguiente lectura sirva para ligar una tarjeta nueva a una solicitud y no como intento de paso.

**Precondiciones:** Operador con sesión; hay una solicitud `Solicitada` seleccionada; el torniquete está en `Cerrado`.

**Criterios de aceptación:**

- **CA1.** Dado un torniquete en `Cerrado` y una solicitud seleccionada, cuando el operador activa el modo registro, entonces el torniquete pasa a `ModoRegistro` y el panel muestra "Acerque la tarjeta".
- **CA2.** Dado un torniquete en `ModoRegistro`, cuando se acerca una tarjeta, entonces no se debita nada, no se abre el brazo y la lectura se procesa según RF-10.
- **CA3.** Dado un torniquete en `ModoRegistro` durante 60 s sin lectura (S6), cuando vence el tiempo, entonces vuelve a `Cerrado` y la solicitud sigue `Solicitada`.
- **CA4.** Dado un torniquete en `EsperandoCruce` o `Alarma`, cuando el operador intenta activar el modo registro, entonces el sistema no lo permite.

**Relacionado con:** RF-10, RF-36 · `Torniquete`, `SoporteTarjeta`

### RF-10 — Ligar el UID y entregar la tarjeta física

**Módulo:** Gestión de tarjetas · **Actor:** Operador de taquilla (a través del ESP32 principal) · **Prioridad:** Must

**Descripción:** El sistema debe ligar el UID leído en modo registro al soporte físico solicitado y marcarlo como entregado.

**Precondiciones:** Torniquete en `ModoRegistro` con una solicitud asociada; el operador verificó el documento del cliente.

**Criterios de aceptación:**

- **CA1.** Dado un UID que no está ligado a ningún soporte, cuando se lee en modo registro, entonces el soporte pasa de `Solicitada` a `Entregada` con ese UID y el torniquete vuelve a `Cerrado`.
- **CA2.** Dado un UID ya ligado a otro soporte de cualquier cliente, cuando se lee en modo registro, entonces no se liga, el panel informa "Tarjeta ya registrada" y el torniquete sigue en `ModoRegistro` para intentar con otra.
- **CA3.** Dado un soporte `Entregada`, cuando ese UID se acerca en modo normal antes de activarse, entonces el paso se rechaza con motivo `SoporteNoActivo`.

**Relacionado con:** RF-09, RF-11 · `SoporteTarjeta`

### RF-11 — Activar la tarjeta física

**Módulo:** Gestión de tarjetas · **Actor:** Cliente · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el cliente confirme desde el portal que recibió su tarjeta física, con lo que queda lista para pasar (S5).

**Precondiciones:** El soporte físico del cliente está `Entregada`.

**Criterios de aceptación:**

- **CA1.** Dado un soporte `Entregada`, cuando el cliente confirma la recepción, entonces pasa a `Activa` y ese UID ya puede validar pasos.
- **CA2.** Dado un cliente con celular `Activo`, cuando activa la tarjeta física, entonces el sistema pide confirmación y, al confirmar, el celular pasa a `Bloqueado` con motivo `Reemplazo` (RF-13).
- **CA3.** Dado un soporte en un estado distinto de `Entregada`, cuando el cliente intenta activarlo, entonces no se permite.

**Relacionado con:** RF-10, RF-13 · `SoporteTarjeta`

### RF-12 — Vincular el celular como tarjeta

**Módulo:** Gestión de tarjetas · **Actor:** Cliente (desde la app Android) · **Prioridad:** Should

**Descripción:** El sistema debe permitir que el cliente convierta su celular Android en soporte virtual de su tarjeta, entregándole a la app una clave secreta de vinculación.

**Prioridad confirmada:** el profesor indicó que el celular como tarjeta no es obligatorio, así que RF-12 y RF-19 quedan en Should.

**Precondiciones:** El cliente inició sesión en la app (RF-02, CA5); el celular tiene NFC con HCE.

**Criterios de aceptación:**

- **CA1.** Dado un cliente sin soporte activo, cuando vincula su celular, entonces se crea un `SoporteTarjeta` virtual `Activo` y la app recibe una clave secreta que no se vuelve a mostrar por ninguna otra vía.
- **CA2.** Dado un cliente con tarjeta física `Activa`, cuando vincula el celular, entonces la app pide confirmación y, al confirmar, la física pasa a `Bloqueada` con motivo `Reemplazo` (S1).
- **CA3.** Dado un cliente con otro celular ya vinculado, cuando vincula uno nuevo, entonces el anterior pasa a `Bloqueado` y su clave deja de servir.
- **CA4.** Dado un celular sin NFC o sin HCE, cuando se intenta vincular, entonces la app informa que no es compatible y sugiere solicitar tarjeta física.

**Relacionado con:** RF-13, RF-14, RF-19 · `SoporteTarjeta`

### RF-13 — Mantener un solo soporte activo

**Módulo:** Gestión de tarjetas · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe garantizar que cada tarjeta tenga como máximo un soporte activo (S1).

**Precondiciones:** Ninguna.

**Criterios de aceptación:**

- **CA1.** Dada cualquier tarjeta, cuando se consulta en cualquier momento, entonces tiene cero o un soporte activo.
- **CA2.** Dada una tarjeta con un soporte activo, cuando se activa otro (RF-11 o RF-12), entonces el anterior pasa a bloqueado con motivo `Reemplazo` en la misma operación y el saldo no cambia.
- **CA3.** Dado un soporte bloqueado por reemplazo, cuando se acerca al torniquete, entonces el paso se rechaza con motivo `SoporteBloqueado`.

**Relacionado con:** RF-11, RF-12, RF-14 · `SoporteTarjeta`

### RF-14 — Bloquear un soporte por pérdida o reporte

**Módulo:** Gestión de tarjetas · **Actor:** Cliente (portal) u Operador de taquilla (panel) · **Prioridad:** Must

**Descripción:** El sistema debe permitir bloquear el soporte activo de una tarjeta, conservando el saldo en la tarjeta lógica.

**Precondiciones:** La tarjeta tiene un soporte activo.

**Criterios de aceptación:**

- **CA1.** Dada una tarjeta con saldo 8.000 y soporte físico `Activa`, cuando el cliente la reporta como perdida, entonces el soporte pasa a `Bloqueada` con motivo `Perdida` y el saldo sigue en 8.000.
- **CA2.** Dado un soporte bloqueado, cuando se acerca al torniquete, entonces el paso se rechaza con motivo `SoporteBloqueado`.
- **CA3.** Dado un soporte bloqueado, cuando el cliente intenta reactivarlo, entonces no se permite; debe pedir reposición (RF-08) o vincular el celular (RF-12).
- **CA4.** Dado un operador que verificó el documento del cliente, cuando bloquea el soporte desde el panel, entonces el resultado es el de CA1 y queda registrado qué operador lo hizo.

**Relacionado con:** RF-08, RF-12, RF-13 · `SoporteTarjeta`, `Operador`

## 5. Módulo 3 — Saldo y recargas

Tres RF (RF-15 a RF-17) cubren cómo entra dinero a la tarjeta y cómo lo ve el cliente. El pago es simulado, pero debe poder aprobarse o rechazarse para probar ambos caminos.

### RF-15 — Recargar saldo con pago simulado

**Módulo:** Saldo y recargas · **Actor:** Cliente · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el cliente recargue el saldo de su tarjeta desde el portal mediante un pago simulado.

**Precondiciones:** Sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un saldo de 1.800, cuando el cliente recarga 10.000 y el pago simulado se aprueba, entonces se registra una `Recarga` aprobada y el saldo queda en 11.800.
- **CA2.** Dado un pago simulado rechazado, cuando el cliente recarga 10.000, entonces se registra una `Recarga` rechazada y el saldo no cambia.
- **CA3.** Dado un monto menor que 1.000 o mayor que 200.000 (S8), cuando el cliente intenta recargar, entonces no se procesa y se indica el rango permitido.
- **CA4.** Dada una tarjeta con el soporte bloqueado o sin soporte, cuando el cliente recarga, entonces la recarga se aplica igual, porque el saldo es de la tarjeta.
- **CA5.** Dada una misma recarga enviada dos veces (doble clic), cuando llegan ambas peticiones, entonces el saldo aumenta una sola vez.

**Relacionado con:** RF-16, RF-17 · `Recarga`, `Tarjeta`

### RF-16 — Consultar saldo y estado de la tarjeta

**Módulo:** Saldo y recargas · **Actor:** Cliente · **Prioridad:** Must

**Descripción:** El sistema debe mostrar al cliente su saldo y el tipo y estado de su soporte, y actualizar el saldo en vivo cuando cambie.

**Precondiciones:** Sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un cliente con sesión, cuando abre el portal, entonces ve su saldo y el tipo y estado de su soporte; por ejemplo, "Tarjeta física — Entregada: confirma que la recibiste".
- **CA2.** Dado el portal abierto, cuando se debita un paso en el torniquete, entonces el saldo mostrado cambia sin recargar la página.
- **CA3.** Dado el portal abierto, cuando se aprueba una recarga, entonces el saldo se actualiza sin recargar la página.

**Relacionado con:** RF-11, RF-15, RF-21 · `Tarjeta`, `SoporteTarjeta`

### RF-17 — Consultar historial de recargas

**Módulo:** Saldo y recargas · **Actor:** Cliente · **Prioridad:** Should

**Descripción:** El sistema debe mostrar al cliente sus recargas, de la más reciente a la más antigua.

**Precondiciones:** Sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un cliente con 3 recargas, cuando abre el historial, entonces ve las 3 con fecha y hora, monto, resultado (aprobada o rechazada) y saldo resultante, la más reciente primero.
- **CA2.** Dado un cliente sin recargas, cuando abre el historial, entonces ve un mensaje de lista vacía.

**Relacionado con:** RF-15 · `Recarga`

## 6. Módulo 4 — Validación de acceso

Diez RF (RF-18 a RF-25, RF-45 y RF-46) cubren el camino desde que la tarjeta toca el lector hasta que el brazo abre o se niega. El ESP32 no decide nada: envía la lectura y ejecuta la orden; la única excepción es el modo seguro sin conexión (RF-25).

### RF-18 — Recibir la lectura de una tarjeta física

**Módulo:** Validación de acceso · **Actor:** ESP32 principal · **Prioridad:** Must

**Descripción:** El sistema debe recibir el UID leído por el ESP32 principal, junto con un identificador de lectura generado por el ESP32, e identificar el soporte y la tarjeta a los que pertenece.

**Precondiciones:** El torniquete está registrado (RF-43) y no está en `ModoRegistro`.

**Criterios de aceptación:**

- **CA1.** Dado un UID ligado a un soporte, cuando el ESP32 lo envía, entonces el servidor identifica la tarjeta y continúa con la validación (RF-20).
- **CA2.** Dado un UID que no pertenece a ningún soporte, cuando el ESP32 lo envía, entonces el servidor responde "denegar" con motivo `TarjetaNoRegistrada`.
- **CA3.** Dada una tarjeta que permanece 3 s sobre el lector, cuando el ESP32 la detecta varias veces seguidas, entonces se procesa una sola lectura y se debita una sola vez.
- **CA4.** Dada una lectura reenviada con el mismo identificador de lectura (reintento del ESP32), cuando llega al servidor, entonces responde lo mismo que a la primera y no debita otra vez.
- **CA5.** Dado un objetivo NFC que indica ISO 14443-4 (bit 0x20 del SAK), cuando el lector lo detecta, entonces el ESP32 intenta primero el SELECT AID de la app y, solo si falla, envía el UID como tarjeta física. Así un celular con la app no se rechaza como `TarjetaNoRegistrada` por su UID aleatorio.

**Relacionado con:** RF-19, RF-20, RF-43, RF-46 · `SoporteTarjeta`, `Torniquete`

### RF-19 — Validar el celular por desafío-respuesta

**Módulo:** Validación de acceso · **Actor:** Cliente (celular), a través del ESP32 principal · **Prioridad:** Should

**Descripción:** El sistema debe identificar un celular vinculado verificando que su respuesta corresponde a un desafío emitido por el servidor (S12), firmada con su clave secreta de vinculación sobre el desafío y el identificador del torniquete.

**Precondiciones:** El celular está vinculado (RF-12).

**Criterios de aceptación:**

- **CA1.** Dado un desafío emitido para T1 en la última consulta del ESP32, cuando un celular con soporte `Activo` responde con su identificador de soporte y la firma correcta, entonces el servidor identifica la tarjeta y continúa con la validación (RF-20).
- **CA2.** Dada una respuesta firmada con otra clave, cuando llega al servidor, entonces responde "denegar" con motivo `FirmaInvalida`.
- **CA3.** Dado un desafío ya usado, con más de 30 s de emitido o que el servidor no emitió, cuando llega una respuesta sobre él, entonces responde "denegar" con motivo `DesafioInvalido`.
- **CA4.** Dada una firma válida calculada para el torniquete T1, cuando se presenta en el torniquete T2, entonces se rechaza con motivo `FirmaInvalida`.
- **CA5.** Dado un celular con soporte `Bloqueado`, cuando responde el desafío, entonces se rechaza con motivo `SoporteBloqueado`.
- **CA6.** Dado un cliente con la sesión de la app vencida o cerrada, cuando acerca el celular, entonces el paso se valida igual, porque la emulación usa la clave de vinculación y no la sesión.

**Relacionado con:** RF-12, RF-20, RF-45 · `SoporteTarjeta`

### RF-20 — Validar torniquete, soporte y saldo

**Módulo:** Validación de acceso · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe decidir si autoriza un paso comprobando, en este orden, que el torniquete esté disponible, que el soporte esté activo y que el saldo cubra la tarifa vigente.

**Precondiciones:** Soporte identificado (RF-18 o RF-19).

**Criterios de aceptación:**

- **CA1.** Dado un torniquete en `Cerrado`, un soporte activo y saldo mayor o igual a la tarifa, cuando se valida, entonces se autoriza el paso (RF-21, RF-22).
- **CA2.** Dado un soporte `Solicitada` o `Entregada`, cuando se valida, entonces se deniega con motivo `SoporteNoActivo`.
- **CA3.** Dado un saldo exactamente igual a la tarifa (3.200), cuando se valida, entonces se autoriza y el saldo queda en 0 (S3).
- **CA4.** Dado un torniquete en `EsperandoCruce`, cuando llega otra lectura, entonces se deniega con motivo `TorniqueteOcupado` y no se debita nada.
- **CA5.** Dado un torniquete en `Alarma`, cuando llega una lectura, entonces se deniega con motivo `TorniqueteEnAlarma`.
- **CA6.** Dado un soporte bloqueado y sin saldo a la vez, cuando se valida, entonces se reporta solo el primer motivo del orden: `SoporteBloqueado`.

**Relacionado con:** RF-18, RF-19, RF-21 · `Tarjeta`, `Tarifa`, `Torniquete`

### RF-21 — Debitar la tarifa al validar el paso

**Módulo:** Validación de acceso · **Actor:** Cliente (a través del ESP32 principal) · **Prioridad:** Must

**Descripción:** El sistema debe descontar la tarifa vigente del saldo de la tarjeta cuando se valida un paso con saldo suficiente.

**Precondiciones:** La tarjeta está activa y ligada a un cliente.

**Criterios de aceptación:**

- **CA1.** Dado un saldo de 5.000 y una tarifa de 3.200, cuando se valida la tarjeta, entonces el saldo queda en 1.800, se registra un `IntentoAcceso` exitoso y el servidor responde "abrir".
- **CA2.** Dado un saldo de 2.000 y una tarifa de 3.200, cuando se valida la tarjeta, entonces el saldo no cambia, se registra un `IntentoAcceso` rechazado con motivo `SaldoInsuficiente` y el servidor responde "denegar".
- **CA3.** Dado que la tarifa cambia de 3.200 a 3.500 (RF-41), cuando se valida un paso después del cambio, entonces se debitan 3.500 y el intento guarda la tarifa cobrada.

**Relacionado con:** RF-20, RF-22, RF-23, RF-41 · `Tarjeta`, `Tarifa`, `IntentoAcceso`

### RF-22 — Abrir el brazo y conceder un paso autorizado

**Módulo:** Validación de acceso · **Actor:** Sistema (orden al ESP32 principal) · **Prioridad:** Must

**Descripción:** El sistema debe, tras autorizar y debitar, ordenar la apertura del brazo y abrir una ventana de exactamente un paso autorizado en ese torniquete.

**Precondiciones:** Paso autorizado y debitado (RF-21).

**Criterios de aceptación:**

- **CA1.** Dado un paso autorizado, cuando el servidor responde al ESP32, entonces la respuesta incluye la acción "abrir", el identificador del paso y el saldo restante; se crea un `PasoAutorizado` `Pendiente` y el torniquete pasa a `EsperandoCruce`.
- **CA2.** Dada la orden "abrir", cuando el ESP32 la recibe, entonces mueve el brazo a la posición abierta.
- **CA3.** Dado un torniquete en `EsperandoCruce`, cuando se consulta su estado, entonces tiene exactamente un `PasoAutorizado` `Pendiente`.
- **CA4.** Dado que el ESP32 abrió el brazo, cuando hace su siguiente consulta, entonces reporta el brazo abierto para ese paso; si no llega ese reporte a tiempo, aplica RF-46.

**Relacionado con:** RF-21, RF-27, RF-29, RF-45, RF-46 · `Torniquete`, `IntentoAcceso`, `PasoAutorizado`

### RF-23 — Registrar cada intento de acceso

**Módulo:** Validación de acceso · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe registrar un `IntentoAcceso` por cada lectura procesada fuera de modo registro, con fecha y hora, torniquete, tarjeta (si se identificó), tipo de soporte, resultado, motivo y tarifa cobrada.

**Precondiciones:** Ninguna.

**Criterios de aceptación:**

- **CA1.** Dado un paso exitoso con tarifa 3.200, cuando termina la validación, entonces existe un intento `Exitoso` con tarifa cobrada 3.200.
- **CA2.** Dado un rechazo por `TarjetaNoRegistrada`, cuando termina la validación, entonces existe un intento `Rechazado` sin tarjeta asociada y con el UID leído.
- **CA3.** Dada una lectura en `ModoRegistro`, cuando se procesa, entonces no se crea ningún `IntentoAcceso`.

**Relacionado con:** RF-28, RF-32, RF-39 · `IntentoAcceso`

### RF-24 — Notificar al cliente el resultado del paso

**Módulo:** Validación de acceso · **Actor:** Sistema · **Prioridad:** Should

**Descripción:** El sistema debe avisar en vivo, en el portal del cliente, cada paso exitoso o rechazado de su tarjeta, con el motivo en lenguaje claro.

**Precondiciones:** El cliente tiene el portal abierto.

**Criterios de aceptación:**

- **CA1.** Dado un paso rechazado por `SaldoInsuficiente`, cuando termina la validación, entonces el portal muestra "Paso rechazado: saldo insuficiente (saldo 2.000, tarifa 3.200)" sin recargar la página.
- **CA2.** Dado un paso exitoso, cuando termina la validación, entonces el portal muestra "Paso registrado — saldo 1.800".

**Relacionado con:** RF-16, RF-23

### RF-25 — Mantener el torniquete cerrado sin conexión

**Módulo:** Validación de acceso · **Actor:** ESP32 principal · **Prioridad:** Must

**Descripción:** El ESP32 principal debe mantener el brazo cerrado y no autorizar pasos cuando no puede comunicarse con el servidor (S2).

**Precondiciones:** Ninguna.

**Criterios de aceptación:**

- **CA1.** Dado el servidor apagado, cuando se acerca una tarjeta, entonces el ESP32 espera como máximo 3 s (S10), no abre el brazo y no queda ningún débito.
- **CA2.** Dado el WiFi caído con el brazo abierto en `EsperandoCruce`, cuando pasan 10 s sin poder comunicarse con el servidor (todas las consultas fallan), entonces el ESP32 cierra el brazo por su cuenta. Es distinto de la expiración normal (RF-29), en la que el ESP32 sí se comunica.
- **CA3.** Dado que vuelve la conexión, cuando se acerca una tarjeta válida, entonces el paso se procesa normalmente sin reiniciar el ESP32.

**Relacionado con:** RF-22, RF-37 · `Torniquete`

### RF-45 — Sincronizar el estado deseado con el ESP32

**Módulo:** Validación de acceso · **Actor:** Sistema y ESP32 principal · **Prioridad:** Must

**Descripción:** El servidor debe mantener el estado deseado de cada torniquete (brazo abierto o cerrado, sirena encendida o apagada), y el ESP32 debe recibirlo en cada consulta, aplicarlo y reportar su estado real (S11). Como se expresa en estados y no en acciones, aplicarlo dos veces no tiene efecto. La apertura por una lectura sigue siendo la respuesta a esa lectura (RF-22). Se agregó en la primera revisión y va al final para no mover la numeración.

**Precondiciones:** Torniquete registrado (RF-43).

**Criterios de aceptación:**

- **CA1.** Dado que el servidor cambia el estado deseado a "brazo cerrado", cuando el ESP32 hace su siguiente consulta (a más tardar 300 ms después), entonces cierra el brazo y en la consulta siguiente reporta el brazo cerrado.
- **CA2.** Dado un estado deseado que el ESP32 ya aplicó, cuando lo recibe otra vez, entonces no hace nada.
- **CA3.** Dado un ESP32 recién reiniciado con el torniquete en `Alarma`, cuando hace su primera consulta, entonces recibe "brazo cerrado, sirena encendida" y lo aplica.
- **CA4.** Dado que el estado reportado no coincide con el deseado durante más de 2 s, cuando el servidor lo detecta, entonces el panel marca el torniquete como desincronizado.

**Relacionado con:** RF-22, RF-27, RF-28, RF-29, RF-34, RF-37, RF-46 · `Torniquete`

### RF-46 — Revertir el débito si el brazo no abrió

**Módulo:** Validación de acceso · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe devolver la tarifa cuando autorizó y debitó un paso, pero el ESP32 no confirma dentro del plazo que abrió el brazo (S15). Así nadie paga sin pasar si la respuesta se pierde. Se agregó en la segunda revisión.

**Precondiciones:** Paso autorizado y debitado (RF-21, RF-22).

**Criterios de aceptación:**

- **CA1.** Dado un paso autorizado a las 08:00:00 con tarifa 3.200, cuando hasta las 08:00:02 el ESP32 no reporta el brazo abierto para ese paso, entonces el `PasoAutorizado` pasa a `Revertido`, el saldo recupera los 3.200 y el torniquete vuelve a `Cerrado`.
- **CA2.** Dado un paso ya consumido por un cruce (RF-27), cuando falta la confirmación de apertura, entonces no se revierte, porque el cruce prueba que la persona pasó.
- **CA3.** Dado un paso revertido, cuando el cliente abre su historial, entonces ve el intento con la nota "Tarifa devuelta: el torniquete no abrió".
- **CA4.** Dado que el ESP32 reintenta la misma lectura tras perder la respuesta, cuando llega con el mismo identificador de lectura, entonces no se cobra de nuevo (RF-18, CA4).

**Relacionado con:** RF-18, RF-21, RF-22, RF-45 · `PasoAutorizado`, `Tarjeta`

## 7. Módulo 5 — Detección de colados

Cinco RF (RF-26 a RF-30) implementan la regla de colados: cada validación abre exactamente un paso, cada cruce de entrada consume uno y un cruce sin paso disponible es fraude.

### RF-26 — Recibir cruces del servicio de visión

**Módulo:** Detección de colados · **Actor:** Servicio de visión · **Prioridad:** Must

**Descripción:** El sistema debe recibir y registrar cada cruce de la línea virtual que reporte el servicio de visión, con torniquete, identificador de seguimiento, sentido y hora de llegada del fotograma al servicio de visión (S13).

**Precondiciones:** El servicio de visión recibe el stream del ESP32-CAM de ese torniquete.

**Criterios de aceptación:**

- **CA1.** Dado un cruce en sentido de entrada, cuando el servicio de visión lo reporta, entonces se registra un `Cruce` y se evalúa según RF-27 o RF-28.
- **CA2.** Dado un cruce en sentido de salida, cuando se reporta, entonces se registra pero no consume pasos ni genera fraude (S7).
- **CA3.** Dado el mismo identificador de seguimiento reportado dos veces, cuando llega el segundo reporte, entonces se cuenta un solo cruce.
- **CA4.** Dado cualquier cruce, cuando se registra, entonces no incluye imágenes ni fotogramas, solo el evento (Ley 1581 de 2012).

**Relacionado con:** RF-27, RF-28 · `Cruce`

### RF-27 — Consumir el paso autorizado con un cruce

**Módulo:** Detección de colados · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe asociar el primer cruce de entrada ocurrido en `EsperandoCruce` al paso autorizado pendiente y ordenar cerrar el brazo.

**Precondiciones:** Torniquete en `EsperandoCruce`.

**Criterios de aceptación:**

- **CA1.** Dado un torniquete en `EsperandoCruce`, cuando llega un cruce de entrada cuya hora de fotograma cae dentro de la ventana del paso, entonces el `PasoAutorizado` pasa a `Consumido` ligado a ese cruce, el servidor ordena "cerrar" (RF-45) y el torniquete vuelve a `Cerrado`.
- **CA2.** Dado un paso consumido, cuando se consulta su intento, entonces muestra el cruce asociado y su hora.

**Relacionado con:** RF-22, RF-26, RF-45 · `Cruce`, `IntentoAcceso`, `Torniquete`

### RF-28 — Detectar un colado y activar la alarma

**Módulo:** Detección de colados · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe marcar como fraude todo cruce de entrada sin paso autorizado disponible, crear una alerta de seguridad, activar la alarma física y mantener el brazo bloqueado.

**Precondiciones:** Ninguna.

**Criterios de aceptación:**

- **CA1.** Dado un torniquete en `Cerrado`, cuando llega un cruce de entrada, entonces se registra un `IntentoAcceso` con resultado `Fraude` y motivo `FraudeSinValidacion`, se crea una `AlertaSeguridad` `Abierta`, se ordena al ESP32 activar la alarma y el torniquete pasa a `Alarma`.
- **CA2.** Dada una validación seguida de dos cruces de entrada (dos personas con una tarjeta), cuando llegan ambos cruces, entonces el primero consume el paso (RF-27) y el segundo produce el resultado de CA1.
- **CA3.** Dado un torniquete en `Alarma`, cuando llegan más cruces de entrada, entonces se ligan a la alerta abierta y no se crean alertas nuevas.
- **CA4.** Dado un torniquete en `Alarma`, cuando se acerca una tarjeta válida, entonces se deniega con motivo `TorniqueteEnAlarma` (RF-20) y el brazo sigue bloqueado.
- **CA5.** Dado un torniquete en `ModoRegistro`, cuando llega un cruce de entrada, entonces se trata como en CA1 y el modo registro se cancela; la solicitud sigue `Solicitada`.

**Relacionado con:** RF-20, RF-33, RF-34, RF-45 · `Cruce`, `IntentoAcceso`, `AlertaSeguridad`

### RF-29 — Expirar el paso autorizado

**Módulo:** Detección de colados · **Actor:** Sistema · **Prioridad:** Must

**Descripción:** El sistema debe anular el paso autorizado y ordenar cerrar el brazo si no ocurre ningún cruce dentro del tiempo límite (S4), esperando la gracia de visión (S13) antes de declararlo expirado.

**Precondiciones:** Torniquete en `EsperandoCruce`.

**Criterios de aceptación:**

- **CA1.** Dado un paso autorizado a las 08:00:00, cuando hasta las 08:00:11 (10 s de ventana más 1 s de gracia) no llega ningún cruce con hora de fotograma anterior a las 08:00:10, entonces el `PasoAutorizado` pasa a `Expirado`, el servidor ordena "cerrar" y el torniquete vuelve a `Cerrado`.
- **CA2.** Dado un cruce con hora de fotograma 08:00:09,5 que llega al servidor a las 08:00:10,6, cuando se evalúa, entonces consume el paso (RF-27) y no se marca como fraude.
- **CA3.** Dado un paso expirado, cuando se consulta la tarjeta, entonces la tarifa no se reembolsa, porque el brazo sí abrió, y el intento sigue `Exitoso` con el paso `Expirado`.
- **CA4.** Dado un cruce de entrada con hora de fotograma posterior a las 08:00:10, cuando llega, entonces se trata como cruce sin validación (RF-28).

**Relacionado con:** RF-22, RF-28, RF-46 · `IntentoAcceso`, `Torniquete`

### RF-30 — Operar sin el servicio de visión

**Módulo:** Detección de colados · **Actor:** Sistema · **Prioridad:** Should

**Descripción:** El sistema debe detectar cuándo el servicio de visión deja de reportar, avisarlo en el panel y seguir validando pasos sin detección de colados.

**Precondiciones:** Ninguna.

**Criterios de aceptación:**

- **CA1.** Dado el servicio de visión sin enviar señal de vida durante 15 s (S14), cuando vence el plazo, entonces el panel muestra "Visión fuera de línea" para ese torniquete.
- **CA2.** Dada la visión fuera de línea, cuando se valida un paso, entonces el brazo abre, se cierra por expiración (RF-29) y el intento queda marcado "sin verificación de visión".
- **CA3.** Dado que la visión vuelve, cuando llega su señal de vida, entonces el panel la muestra otra vez en línea.

**Relacionado con:** RF-29, RF-37 · `Torniquete`

## 8. Módulo 6 — Monitoreo y operación

Ocho RF (RF-31 a RF-38) cubren el panel del personal: ver el sistema en vivo, atender alertas y entregar tarjetas. La clasificación de alertas (RF-35) además produce los datos para medir la precisión de la visión en los RNF.

### RF-31 — Mostrar el estado del torniquete en vivo

**Módulo:** Monitoreo y operación · **Actor:** Operador, Personal de seguridad · **Prioridad:** Must

**Descripción:** El sistema debe mostrar en el panel el estado actual de cada torniquete y actualizarlo sin recargar la página.

**Precondiciones:** Usuario del panel con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado el panel abierto, cuando un torniquete pasa de `Cerrado` a `EsperandoCruce`, entonces el panel muestra el nuevo estado sin recargar la página.
- **CA2.** Dado un torniquete en `Alarma`, cuando se ve el panel, entonces se distingue del resto por color o ícono y muestra desde qué hora está en alarma.
- **CA3.** Dados varios torniquetes registrados, cuando se abre el panel, entonces aparecen todos con nombre, estación y estado.

**Relacionado con:** RF-22, RF-28, RF-43 · `Torniquete`

### RF-32 — Mostrar el historial de intentos de acceso

**Módulo:** Monitoreo y operación · **Actor:** Operador, Personal de seguridad · **Prioridad:** Must

**Descripción:** El sistema debe listar los intentos de acceso de todos los torniquetes, del más reciente al más antiguo, con filtros por fecha, torniquete y resultado.

**Precondiciones:** Usuario del panel con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado el panel abierto, cuando ocurre un intento nuevo, entonces aparece al inicio de la lista sin recargar la página.
- **CA2.** Dado el filtro resultado `Rechazado` y fecha de hoy, cuando se aplica, entonces solo se ven los rechazos de hoy, cada uno con su motivo.
- **CA3.** Dado un intento de un cliente identificado, cuando se muestra en el panel, entonces aparece su nombre y el documento parcialmente oculto (solo los 4 últimos dígitos).

**Relacionado con:** RF-23 · `IntentoAcceso`

### RF-33 — Notificar alertas en tiempo real

**Módulo:** Monitoreo y operación · **Actor:** Sistema (hacia el personal de seguridad) · **Prioridad:** Must

**Descripción:** El sistema debe mostrar cada alerta de seguridad nueva en el panel en cuanto se crea, con aviso visual y sonoro.

**Precondiciones:** Ninguna.

**Criterios de aceptación:**

- **CA1.** Dado el panel abierto, cuando se crea una `AlertaSeguridad`, entonces aparece en la lista de alertas abiertas con torniquete, hora y número de cruces, sin recargar la página.
- **CA2.** Dado el panel abierto en otra vista, cuando se crea la alerta, entonces suena un aviso en el navegador y se ve un indicador con el número de alertas abiertas.
- **CA3.** Dado el panel cerrado cuando se creó la alerta, cuando se abre después, entonces la alerta aparece porque sigue `Abierta`.

**Relacionado con:** RF-28, RF-34 · `AlertaSeguridad`

### RF-34 — Apagar la alarma desde el panel

**Módulo:** Monitoreo y operación · **Actor:** Personal de seguridad · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el personal de seguridad apague la alarma física de un torniquete desde el panel y lo devuelva a operación.

**Precondiciones:** Torniquete en `Alarma` con una alerta `Abierta`.

**Criterios de aceptación:**

- **CA1.** Dado un torniquete en `Alarma`, cuando el personal pulsa "Apagar alarma", entonces el servidor ordena al ESP32 desactivar la sirena, el torniquete pasa a `Cerrado` y la alerta pasa a `Atendida` con quién la atendió y a qué hora.
- **CA2.** Dado un operador de taquilla, cuando intenta apagar la alarma, entonces no se le permite (RF-06).
- **CA3.** Dado un torniquete recién devuelto a `Cerrado`, cuando se acerca una tarjeta válida, entonces el paso se procesa normalmente.

**Relacionado con:** RF-06, RF-28, RF-35, RF-45 · `AlertaSeguridad`, `Torniquete`

### RF-35 — Resolver y clasificar una alerta

**Módulo:** Monitoreo y operación · **Actor:** Personal de seguridad · **Prioridad:** Must

**Descripción:** El sistema debe permitir cerrar una alerta atendida indicando si fue un colado confirmado o un falso positivo, con una observación.

**Precondiciones:** Alerta en estado `Atendida`.

**Criterios de aceptación:**

- **CA1.** Dada una alerta `Atendida`, cuando el personal la clasifica como "Colado confirmado" con la observación "Persona retenida en taquilla", entonces la alerta pasa a `Resuelta` con esa clasificación y esa observación.
- **CA2.** Dada una alerta clasificada como "Falso positivo", cuando queda `Resuelta`, entonces suma al total de falsos positivos del sistema.
- **CA3.** Dada una alerta `Abierta`, cuando se intenta resolver, entonces el sistema pide apagar primero la alarma (RF-34).

**Relacionado con:** RF-34 · `AlertaSeguridad`

### RF-36 — Gestionar solicitudes de tarjeta física

**Módulo:** Monitoreo y operación · **Actor:** Operador de taquilla · **Prioridad:** Must

**Descripción:** El sistema debe listar las solicitudes de tarjeta física pendientes y permitir buscarlas por documento del cliente para iniciar la entrega.

**Precondiciones:** Operador con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dadas 3 solicitudes `Solicitada`, cuando el operador abre la lista, entonces ve las 3 con nombre del cliente, documento y fecha de solicitud, la más antigua primero.
- **CA2.** Dado un número de documento, cuando el operador lo busca, entonces aparece la solicitud de ese cliente o el mensaje "Sin solicitud pendiente".
- **CA3.** Dada una solicitud seleccionada, cuando el operador pulsa "Entregar", entonces se inicia el modo registro (RF-09) para ese cliente.
- **CA4.** Dada una solicitud cuyo UID se ligó (RF-10), cuando se actualiza la lista, entonces ya no aparece entre las pendientes.

**Relacionado con:** RF-08, RF-09, RF-10 · `SoporteTarjeta`

### RF-37 — Mostrar el estado de conexión de los dispositivos

**Módulo:** Monitoreo y operación · **Actor:** Operador, Personal de seguridad · **Prioridad:** Should

**Descripción:** El sistema debe mostrar si el ESP32 principal de cada torniquete está en línea, tomando cada consulta de órdenes (RF-45) como señal de vida. La visión se cubre en RF-30.

**Precondiciones:** Torniquete registrado (RF-43).

**Criterios de aceptación:**

- **CA1.** Dado un ESP32 sin señal de vida durante 5 s (S14), cuando vence el plazo, entonces el panel lo muestra "Fuera de línea" con la hora de la última señal.
- **CA2.** Dado que el ESP32 vuelve a reportar, cuando llega su señal, entonces el panel lo muestra "En línea" sin recargar la página.

**Relacionado con:** RF-25, RF-30 · `Torniquete`

### RF-38 — Ver el video en vivo del torniquete

**Módulo:** Monitoreo y operación · **Actor:** Personal de seguridad · **Prioridad:** Could

**Descripción:** El sistema debe permitir ver en el panel el video en vivo del torniquete, con las personas detectadas y la línea virtual superpuestas, sin guardarlo. También sirve para calibrar la cámara en la Fase 3.

**Precondiciones:** Visión en línea para ese torniquete.

**Criterios de aceptación:**

- **CA1.** Dado un torniquete con visión en línea, cuando el personal abre su vista de video, entonces ve el video en vivo con recuadros sobre las personas y la línea virtual dibujada.
- **CA2.** Dado que el personal cierra la vista, cuando se revisa el servidor, entonces no quedó guardado ningún archivo de video ni imagen.

**Relacionado con:** RF-26 · `Torniquete`

## 9. Módulo 7 — Historial del cliente

Dos RF (RF-39 y RF-40). El historial pertenece a la tarjeta lógica, no al soporte, así que sobrevive a bloqueos y reemplazos.

### RF-39 — Consultar historial de viajes

**Módulo:** Historial del cliente · **Actor:** Cliente · **Prioridad:** Must

**Descripción:** El sistema debe mostrar al cliente los intentos de acceso de su tarjeta, del más reciente al más antiguo: los exitosos con tarifa y saldo resultante, y los rechazados con su motivo.

**Precondiciones:** Sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un cliente con 2 pasos exitosos, cuando abre el historial, entonces ve ambos con fecha y hora, torniquete y estación, tarifa cobrada y saldo resultante.
- **CA2.** Dado un paso rechazado por `SaldoInsuficiente`, cuando abre el historial, entonces aparece marcado como rechazado con el motivo en lenguaje claro.
- **CA3.** Dado un cliente que cambió de soporte por bloqueo o reemplazo, cuando abre el historial, entonces sigue viendo los viajes hechos con el soporte anterior.
- **CA4.** Dado el historial abierto, cuando se registra un paso nuevo de su tarjeta, entonces aparece al inicio de la lista sin recargar la página.

**Relacionado con:** RF-06, RF-23 · `IntentoAcceso`, `Tarjeta`

### RF-40 — Filtrar el historial por fechas

**Módulo:** Historial del cliente · **Actor:** Cliente · **Prioridad:** Could

**Descripción:** El sistema debe permitir que el cliente filtre su historial de viajes por un rango de fechas.

**Precondiciones:** Sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado el rango del 1 al 15 de octubre, cuando el cliente aplica el filtro, entonces solo ve los intentos de ese rango.
- **CA2.** Dado un rango sin viajes, cuando aplica el filtro, entonces ve un mensaje de lista vacía.

**Relacionado con:** RF-39 · `IntentoAcceso`

## 10. Módulo 8 — Administración

Cuatro RF (RF-41 a RF-44) cubren la configuración del sistema. Para la demo, lo mínimo es la tarifa; operadores y torniquetes pueden cargarse como datos semilla si falta tiempo.

### RF-41 — Configurar la tarifa

**Módulo:** Administración · **Actor:** Administrador · **Prioridad:** Must

**Descripción:** El sistema debe permitir que el administrador fije el valor de la tarifa por paso, conservando el historial de tarifas anteriores.

**Precondiciones:** Administrador con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dada una tarifa vigente de 3.200, cuando el administrador la cambia a 3.500, entonces los pasos validados desde ese momento cobran 3.500 (RF-21, CA3).
- **CA2.** Dado un cambio de tarifa, cuando se consultan intentos anteriores, entonces conservan la tarifa cobrada en su momento.
- **CA3.** Dado un valor de 0 o negativo, cuando el administrador intenta guardarlo, entonces no se acepta.
- **CA4.** Dado cualquier cambio de tarifa, cuando se guarda, entonces queda registrado quién lo hizo y desde cuándo rige.

**Relacionado con:** RF-20, RF-21 · `Tarifa`

### RF-42 — Gestionar operadores

**Módulo:** Administración · **Actor:** Administrador · **Prioridad:** Should

**Descripción:** El sistema debe permitir que el administrador cree, edite y desactive cuentas de operador de taquilla y de personal de seguridad.

**Precondiciones:** Administrador con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un administrador, cuando crea un operador con rol Seguridad, entonces esa persona puede iniciar sesión y apagar alarmas.
- **CA2.** Dado un operador desactivado, cuando intenta iniciar sesión, entonces no entra (RF-02, CA4) y sus acciones pasadas siguen en el historial con su nombre.
- **CA3.** Dado el primer arranque sin ningún administrador, cuando inicia el servidor, entonces se crea un administrador inicial desde la configuración.

**Relacionado con:** RF-02, RF-06 · `Operador`

### RF-43 — Registrar torniquetes

**Módulo:** Administración · **Actor:** Administrador · **Prioridad:** Should

**Descripción:** El sistema debe permitir registrar torniquetes con nombre y estación, y dar a cada uno una credencial con la que su ESP32 y su servicio de visión se identifican ante la API. La credencial del torniquete de la demo existe desde la Fase 1 como dato semilla, igual que el administrador inicial (RF-42, CA3); lo que queda en Should es la pantalla de registro.

**Precondiciones:** Administrador con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dado un torniquete nuevo "T1 — Estación Central", cuando el administrador lo registra, entonces aparece en el panel en `Cerrado` y se genera su credencial.
- **CA2.** Dada una lectura o un cruce con credencial inválida, cuando llega a la API, entonces se rechaza sin procesarse ni registrar un intento.
- **CA3.** Dado un torniquete desactivado, cuando su ESP32 envía una lectura, entonces se rechaza igual que en CA2.

**Relacionado con:** RF-18, RF-26, RF-31 · `Torniquete`

### RF-44 — Configurar parámetros de operación

**Módulo:** Administración · **Actor:** Administrador · **Prioridad:** Could

**Descripción:** El sistema debe permitir cambiar sin recompilar los tiempos y límites de operación: expiración del paso, duración del modo registro, montos de recarga y plazos de señal de vida. El archivo de configuración da los valores por defecto; lo que el administrador guarda desde el panel queda en la base de datos y prevalece.

**Precondiciones:** Administrador con sesión iniciada.

**Criterios de aceptación:**

- **CA1.** Dada la expiración en 10 s, cuando el administrador la cambia a 8 s, entonces los pasos autorizados desde ese momento expiran a los 8 s.
- **CA2.** Dado un valor fuera de rango (expiración de 0 s o de más de 60 s), cuando intenta guardarlo, entonces no se acepta.

**Relacionado con:** RF-09, RF-15, RF-29, RF-30, RF-37

## 11. Fuera de alcance (Won't)

Estas funciones quedan fuera de la versión 1 de forma explícita, para que el alcance no crezca durante el desarrollo.

| Funcionalidad | Motivo |
| --- | --- |
| Pasarela de pago real | Decisión 6: el pago es simulado |
| iPhone o app iOS como tarjeta | Decisión 5: Apple restringe la emulación de tarjetas |
| Guardar video o imágenes | Decisión 8: privacidad (Ley 1581 de 2012) |
| Recarga en efectivo en taquilla | El objetivo es evitar la taquilla; la recarga es web |
| Tarifas diferenciales, transbordos o subsidios | S3: tarifa fija por paso |
| Validación sin servidor (listas locales en el ESP32) | El servidor es la única fuente de verdad (S2) |
| Control del torniquete de salida | S7: solo se controla la entrada |
| Varios soportes activos por cliente | S1 |
| Notificaciones push al celular | El portal se actualiza en vivo mientras está abierto |
| Cancelar una solicitud de tarjeta física en estado Solicitada | Aporta poco a la demo; si el cliente no la recoge, la solicitud simplemente queda pendiente |
| Liberación de emergencia del brazo | Limitación del prototipo: el torniquete queda cerrado ante fallas (S2), pero uno real debe poder liberarse en una evacuación |

## 12. Decisiones de las revisiones

Dos revisiones cerraron 19 puntos; todos ya están aplicados en los RF y en los supuestos.

### Primera revisión

| # | Punto | Decisión | RF afectados |
| --- | --- | --- | --- |
| 1 | Comunicación servidor → ESP32 | El ESP32 consulta cada 300 ms; desde la segunda revisión recibe un estado deseado en lugar de órdenes (S11) | RF-45, RF-27, RF-28, RF-29, RF-34 |
| 2 | Estados del torniquete | `ModoRegistro` es un estado; la conexión es un atributo aparte (hora de la última señal) | RF-09, RF-37 |
| 3 | Paso autorizado | Entidad nueva `PasoAutorizado`; `IntentoAcceso` queda como registro histórico | RF-22, RF-27, RF-29 |
| 4 | Desafío del celular | Lo genera el servidor y viaja en la consulta; un solo uso, expira a los 30 s; la firma incluye el id del torniquete (S12) | RF-19 |
| 5 | Prioridad del celular | Queda en Should: el profesor confirmó que no es obligatorio | RF-12, RF-19 |
| 6 | Latencia de visión | Los cruces se juzgan por la hora del fotograma, con gracia (S13) | RF-26, RF-27, RF-29 |

### Segunda revisión

| # | Punto | Decisión | RF afectados |
| --- | --- | --- | --- |
| 7 | Cobro sin paso | Identificador por lectura; el débito se revierte si el ESP32 no confirma la apertura en 2 s (S15) | RF-18, RF-22, RF-46 (nuevo) |
| 8 | Celular frente a tarjeta | Si el objetivo indica ISO 14443-4, se intenta el SELECT AID antes de usar el UID | RF-18 |
| 9 | Forma de las órdenes | Estado deseado en lugar de acciones; aplicarlo dos veces no tiene efecto y resuelve el reinicio | RF-45 |
| 10 | Sesión y NFC | La emulación usa la clave de vinculación, no la sesión | RF-19 |
| 11 | Gracia de visión | Máximo de al menos 100 mediciones del retraso completo, más un margen | S13, RF-26 |
| 12 | Pérdida de comunicación | RF-25 cuenta 10 s sin comunicación, no 10 s sin órdenes | RF-25 |
| 13 | Cruce en `ModoRegistro` | Es fraude y cancela el modo registro | RF-28 |
| 14 | Motivos de bloqueo | Catálogo `Perdida`, `Reporte`, `Reemplazo` | Sección 1, RF-13, RF-14 |
| 15 | Datos semilla | Credencial del torniquete y administrador inicial existen desde la Fase 1 | RF-42, RF-43 |
| 16 | Códigos HTTP | 401 sin sesión, 403 sin permiso, 404 para recursos de otro cliente | RF-06 |
| 17 | Configuración | El archivo da los valores por defecto; la base de datos prevalece | RF-44 |
| 18 | Autorización de datos | Criterio nuevo en RF-01, pedido por RNF-28 | RF-01 |
| 19 | Cancelar solicitud y evacuación | Quedan fuera de alcance | Sección 11 |

## 13. Matriz resumen

La Fase 2 concentra 29 de los 46 RF, así que conviene repartirla entre los dos frentes; las fases 1, 3, 4 y 5 tienen 4, 10, 2 y 1 RF.

| RF | Nombre | Módulo | Prioridad | Fase |
| --- | --- | --- | --- | --- |
| RF-01 | Registrar cliente | Clientes | Must | 2 |
| RF-02 | Iniciar sesión | Clientes | Must | 2 |
| RF-03 | Cerrar sesión | Clientes | Must | 2 |
| RF-04 | Recuperar contraseña | Clientes | Should | 2 |
| RF-05 | Editar datos personales | Clientes | Should | 2 |
| RF-06 | Restringir el acceso por rol y por propietario | Clientes | Must | 2 |
| RF-07 | Crear la tarjeta lógica al registrarse | Tarjetas | Must | 2 |
| RF-08 | Solicitar tarjeta física | Tarjetas | Must | 2 |
| RF-09 | Activar el modo registro en el lector | Tarjetas | Must | 2 |
| RF-10 | Ligar el UID y entregar la tarjeta física | Tarjetas | Must | 2 |
| RF-11 | Activar la tarjeta física | Tarjetas | Must | 2 |
| RF-12 | Vincular el celular como tarjeta | Tarjetas | Should | 4 |
| RF-13 | Mantener un solo soporte activo | Tarjetas | Must | 2 |
| RF-14 | Bloquear un soporte por pérdida o reporte | Tarjetas | Must | 2 |
| RF-15 | Recargar saldo con pago simulado | Saldo | Must | 2 |
| RF-16 | Consultar saldo y estado de la tarjeta | Saldo | Must | 2 |
| RF-17 | Consultar historial de recargas | Saldo | Should | 2 |
| RF-18 | Recibir la lectura de una tarjeta física | Validación | Must | 1 |
| RF-19 | Validar el celular por desafío-respuesta | Validación | Should | 4 |
| RF-20 | Validar torniquete, soporte y saldo | Validación | Must | 2 |
| RF-21 | Debitar la tarifa al validar el paso | Validación | Must | 2 |
| RF-22 | Abrir el brazo y conceder un paso autorizado | Validación | Must | 1 |
| RF-23 | Registrar cada intento de acceso | Validación | Must | 2 |
| RF-24 | Notificar al cliente el resultado del paso | Validación | Should | 2 |
| RF-25 | Mantener el torniquete cerrado sin conexión | Validación | Must | 1 |
| RF-26 | Recibir cruces del servicio de visión | Colados | Must | 3 |
| RF-27 | Consumir el paso autorizado con un cruce | Colados | Must | 3 |
| RF-28 | Detectar un colado y activar la alarma | Colados | Must | 3 |
| RF-29 | Expirar el paso autorizado | Colados | Must | 3 |
| RF-30 | Operar sin el servicio de visión | Colados | Should | 3 |
| RF-31 | Mostrar el estado del torniquete en vivo | Monitoreo | Must | 2 |
| RF-32 | Mostrar el historial de intentos de acceso | Monitoreo | Must | 2 |
| RF-33 | Notificar alertas en tiempo real | Monitoreo | Must | 3 |
| RF-34 | Apagar la alarma desde el panel | Monitoreo | Must | 3 |
| RF-35 | Resolver y clasificar una alerta | Monitoreo | Must | 3 |
| RF-36 | Gestionar solicitudes de tarjeta física | Monitoreo | Must | 2 |
| RF-37 | Mostrar el estado de conexión de los dispositivos | Monitoreo | Should | 2 |
| RF-38 | Ver el video en vivo del torniquete | Monitoreo | Could | 3 |
| RF-39 | Consultar historial de viajes | Historial | Must | 2 |
| RF-40 | Filtrar el historial por fechas | Historial | Could | 5 |
| RF-41 | Configurar la tarifa | Administración | Must | 2 |
| RF-42 | Gestionar operadores | Administración | Should | 2 |
| RF-43 | Registrar torniquetes | Administración | Should | 2 |
| RF-44 | Configurar parámetros de operación | Administración | Could | 3 |
| RF-45 | Sincronizar el estado deseado con el ESP32 | Validación | Must | 1 |
| RF-46 | Revertir el débito si el brazo no abrió | Validación | Must | 2 |
