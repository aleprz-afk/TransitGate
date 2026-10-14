# TransitGate — Historias de usuario (v1.1)

Oct 10, 2026 · @alex

**Cambios en v1.1 (14 oct 2026):** se alinean seis criterios con el MVP, el contrato y el plan. HU-19 CA2, HU-22 CA2 y HU-34 CA2 quedan marcados para después del MVP o para la semana en que existe lo que necesitan. HU-19 CA3 pasa a decir lo que permite el contrato. HU-22 aclara qué pasa con la alerta en el MVP. HU-35 deja el 401 a HU-28, porque `X-Device-Key` está en modo tolerante hasta después del H1. HU-20 indica de dónde salen la hora del estado y la de la última señal (evento `EstadoTorniquete`).

## Alcance y convenciones

Este documento traduce los 46 RF v2 a 35 historias de usuario: 25 Must, 8 Should y 2 Could. Sirven para el tablero del equipo (una tarjeta por historia) y para explicar el sistema desde el punto de vista de quien lo usa. Los criterios completos siguen en los RF v2; aquí van solo los que definen si la historia está terminada.

**Formato.** Cada historia sigue la plantilla *Como \[actor\], quiero \[acción\], para \[beneficio\]*, con 2 a 4 criterios de aceptación en formato Dado / Cuando / Entonces y su trazabilidad a RF y RNF.

**Prioridad.** MoSCoW, con el mismo significado que en los RF v2: Must = sin ella la demo no cumple; Should = importante, pero la demo funciona sin ella; Could = si sobra tiempo.

**Estimación.** Puntos de historia en escala Fibonacci, relativos entre sí:

| Puntos | Referencia |
| --- | --- |
| 1 | Cambio pequeño, menos de medio día |
| 2–3 | Un día de trabajo de una persona |
| 5 | Varios días o toca dos capas (por ejemplo, API y web) |
| 8 | Integra hardware, visión o tiempo real |
| 13 | Demasiado grande: se divide antes de empezar |

**INVEST.** Cada historia debe ser independiente, negociable, valiosa, estimable, pequeña y verificable. Las que no cumplen "pequeña" están marcadas para dividirse.

**Definición de terminado.** La misma del plan de trabajo: los criterios pasan con prueba automatizada o video corto, el simulador está al día si se tocó un contrato y la documentación afectada está actualizada.

## Épicas

Las 35 historias se agrupan en 8 épicas, que coinciden con los módulos de los RF v2 más una de trabajo habilitador del equipo.

| Épica | Objetivo | Historias |
| --- | --- | --- |
| E1 — Cuenta y acceso | El cliente crea y administra su cuenta desde el celular; cada rol ve solo lo suyo | HU-01 a HU-04, HU-30 |
| E2 — Tarjeta | El cliente obtiene su tarjeta sin hacer la fila de compra y puede bloquearla | HU-05, HU-06, HU-14, HU-15, HU-16, HU-17, HU-18 |
| E3 — Saldo | El cliente recarga y ve su saldo y sus movimientos en vivo | HU-07, HU-08, HU-12, HU-13 |
| E4 — Paso por el torniquete | El brazo abre en menos de 1 s con una tarjeta válida y nadie paga sin pasar | HU-09, HU-10, HU-11, HU-31, HU-33 |
| E5 — Detección de colados | Dos personas con una sola validación disparan la alarma | HU-21, HU-24, HU-25 |
| E6 — Operación y monitoreo | El personal ve el sistema en vivo y atiende las alertas desde el celular | HU-19, HU-20, HU-22, HU-23 |
| E7 — Administración | El administrador ajusta tarifa, personal, torniquetes y parámetros sin tocar código | HU-26 a HU-29 |
| E8 — Habilitadoras | El equipo trabaja en paralelo y el torniquete se recupera solo de fallas | HU-32, HU-34, HU-35 |

## Historias del cliente

Quince historias cubren lo que la persona que viaja hace desde su celular y en el torniquete; HU-09 es la central del proyecto.

**HU-01 · Crear mi cuenta desde el celular** — Must · 5 pts

Como persona que llega a la estación sin tarjeta, quiero crear mi cuenta desde el celular, para no tener que hacer la fila de la taquilla.

- Dado un formulario con datos válidos y la autorización de datos marcada, cuando envío el registro, entonces queda mi cuenta, una tarjeta con saldo 0 y la sesión iniciada.
- Dado un correo o documento ya registrado, cuando envío el registro, entonces se me indica cuál dato está repetido.
- Dada la casilla de autorización sin marcar, cuando envío el registro, entonces no se crea la cuenta.

Trazabilidad: RF-01, RF-07 · RNF-17, RNF-27, RNF-28

**HU-02 · Entrar y salir de mi cuenta** — Must · 3 pts

Como cliente, quiero iniciar y cerrar sesión con mi correo y contraseña, para que solo yo pueda ver y recargar mi tarjeta.

- Dadas credenciales correctas, cuando inicio sesión, entonces veo mi saldo.
- Dada una contraseña incorrecta, cuando intento entrar, entonces el mensaje no dice si falló el correo o la contraseña.
- Dado que cierro sesión, cuando abro una página protegida, entonces me pide iniciar sesión otra vez.

Trazabilidad: RF-02, RF-03 · RNF-18, RNF-22

**HU-03 · Recuperar mi contraseña** — Should · 3 pts

Como cliente que olvidó su contraseña, quiero recibir un enlace para definir una nueva, para no perder el acceso a mi saldo.

- Dado un correo registrado, cuando pido recuperar, entonces se genera un enlace de un solo uso válido 30 min.
- Dado un correo no registrado, cuando pido recuperar, entonces veo el mismo mensaje y no se envía nada.

Trazabilidad: RF-04 · S9

**HU-04 · Mantener mis datos al día** — Should · 2 pts

Como cliente, quiero cambiar mi teléfono, correo y contraseña, para que mi cuenta tenga datos vigentes.

- Dado un cambio de contraseña, cuando guardo, entonces se me exige la contraseña actual.
- Dado cualquier cliente, cuando intento cambiar mi documento, entonces no se permite.

Trazabilidad: RF-05

**HU-05 · Pedir mi tarjeta física por la web** — Must · 2 pts

Como cliente sin celular Android compatible, quiero solicitar una tarjeta física desde el portal, para solo tener que recogerla en taquilla.

- Dado que no tengo tarjeta física, cuando la solicito, entonces queda en estado Solicitada y aparece en el panel de taquilla.
- Dada una solicitud ya pendiente, cuando intento pedir otra, entonces no se permite.

Trazabilidad: RF-08

**HU-06 · Activar la tarjeta que recibí** — Must · 2 pts

Como cliente que recogió su tarjeta, quiero confirmar en el portal que la recibí, para que empiece a funcionar en el torniquete.

- Dada mi tarjeta Entregada, cuando confirmo la recepción, entonces pasa a Activa y ya puedo pasar.
- Dado que tenía el celular activo, cuando activo la física, entonces se me pide confirmar y el celular queda bloqueado por reemplazo.

Trazabilidad: RF-11, RF-13 · S5

**HU-07 · Recargar saldo desde el celular** — Must · 5 pts

Como cliente, quiero recargar mi tarjeta desde el portal, para no depender de un punto de recarga.

- Dado un saldo de 1.800, cuando recargo 10.000 y el pago simulado se aprueba, entonces el saldo queda en 11.800.
- Dado un monto fuera de 1.000–200.000, cuando intento recargar, entonces se me indica el rango.
- Dado un doble clic en Recargar, cuando llegan las dos peticiones, entonces el saldo aumenta una sola vez.

Trazabilidad: RF-15 · RNF-16, S8

**HU-08 · Ver mi saldo en vivo** — Must · 3 pts

Como cliente, quiero ver mi saldo y el estado de mi tarjeta actualizados sin recargar la página, para saber al instante cuánto me queda.

- Dado el portal abierto, cuando pago un paso o se aprueba una recarga, entonces el saldo cambia en menos de 2 s.
- Dada una tarjeta Entregada, cuando abro el portal, entonces veo que debo confirmar la recepción.

Trazabilidad: RF-16 · RNF-05

**HU-09 · Pasar el torniquete con mi tarjeta** — Must · 8 pts

Como cliente con saldo, quiero acercar mi tarjeta y que el brazo abra enseguida, para entrar a la estación sin demoras.

- Dado un saldo de 5.000 y tarifa de 3.200, cuando acerco la tarjeta, entonces el brazo empieza a abrir en menos de 1 s y el saldo queda en 1.800.
- Dado que crucé, cuando la cámara registra mi paso, entonces el brazo se cierra.
- Dado que dejo la tarjeta 3 s sobre el lector, cuando el lector la detecta varias veces, entonces se cobra una sola vez.

Trazabilidad: RF-18, RF-20, RF-21, RF-22, RF-23, RF-27 · RNF-01, RNF-03, RNF-15

**HU-10 · Saber por qué no pude pasar** — Should · 2 pts

Como cliente rechazado en el torniquete, quiero ver el motivo en mi celular en lenguaje claro, para saber qué hacer.

- Dado un rechazo por saldo, cuando termina la validación, entonces el portal muestra el saldo, la tarifa y el motivo sin recargar.
- Dado cualquier motivo del catálogo, cuando se muestra, entonces está en español y sin códigos internos.

Trazabilidad: RF-24, RF-20 · RNF-32

**HU-11 · No pagar si el torniquete no abrió** — Must · 5 pts

Como cliente, quiero que me devuelvan la tarifa si me cobraron pero el brazo no abrió, para no pagar sin entrar.

- Dado un paso cobrado, cuando el ESP32 no confirma la apertura en 2 s, entonces el saldo recupera la tarifa.
- Dado un paso revertido, cuando abro mi historial, entonces veo la nota "Tarifa devuelta: el torniquete no abrió".
- Dado un paso en el que sí crucé, cuando falta la confirmación, entonces no se revierte.

Trazabilidad: RF-46, RF-18 CA4 · S15

**HU-12 · Revisar mis viajes** — Must · 3 pts

Como cliente, quiero ver mis viajes con fecha, estación y tarifa, para controlar en qué gasto mi saldo.

- Dados 2 pasos exitosos, cuando abro el historial, entonces veo ambos con fecha en hora de Colombia, torniquete, tarifa y saldo resultante.
- Dado que cambié de soporte, cuando abro el historial, entonces sigo viendo los viajes anteriores.
- (Could) Dado un rango de fechas, cuando filtro, entonces solo veo los viajes de ese rango.

Trazabilidad: RF-39, RF-40 · RNF-42

**HU-13 · Revisar mis recargas** — Should · 2 pts

Como cliente, quiero ver mis recargas aprobadas y rechazadas, para comprobar que mis pagos llegaron.

- Dadas 3 recargas, cuando abro el historial, entonces las veo con fecha, monto, resultado y saldo resultante, la más reciente primero.

Trazabilidad: RF-17

**HU-14 · Bloquear mi tarjeta si la pierdo** — Must · 3 pts

Como cliente que perdió su tarjeta, quiero bloquearla desde el portal, para que nadie use mi saldo.

- Dada una tarjeta con 8.000 de saldo, cuando la reporto como perdida, entonces queda Bloqueada y el saldo sigue en 8.000.
- Dada una tarjeta bloqueada, cuando alguien la acerca al torniquete, entonces se rechaza con motivo SoporteBloqueado.
- Dada una tarjeta bloqueada, cuando quiero seguir viajando, entonces puedo pedir reposición sin perder el saldo.

Trazabilidad: RF-14, RF-08 CA3

**HU-15 · Usar mi celular como tarjeta** — Should · 13 pts (dividir) · carril opcional

Como cliente con Android, quiero pasar el torniquete acercando mi celular, para no tener que cargar una tarjeta.

- Dado mi celular vinculado, cuando lo acerco, entonces el brazo abre en menos de 1,5 s.
- Dada una respuesta grabada de otro paso o de otro torniquete, cuando se presenta, entonces se rechaza.
- Dado un celular sin NFC o sin HCE, cuando intento vincularlo, entonces la app me sugiere pedir tarjeta física.

Se divide en: vincular el celular (RF-12) y validar por desafío-respuesta (RF-19). Trazabilidad: RF-12, RF-13, RF-19 · RNF-02, RNF-21, RNF-34

## Historias del operador de taquilla

Cuatro historias cubren la entrega de tarjetas en taquilla; HU-17 es la que reemplaza la fila de compra por una simple recogida.

**HU-16 · Encontrar las solicitudes pendientes** — Must · 3 pts

Como operador de taquilla, quiero ver las solicitudes de tarjeta y buscarlas por documento, para atender rápido a quien llega a recogerla.

- Dadas 3 solicitudes pendientes, cuando abro la lista, entonces las veo con nombre, documento y fecha, la más antigua primero.
- Dado un número de documento, cuando lo busco, entonces aparece su solicitud o "Sin solicitud pendiente".
- Dada una solicitud nueva, cuando el cliente la crea, entonces aparece en el panel sin recargar.

Trazabilidad: RF-36 · RNF-27

**HU-17 · Entregar una tarjeta con el lector** — Must · 5 pts

Como operador, quiero poner el lector en modo registro y acercar una tarjeta nueva, para ligarla a la cuenta del cliente sin cobrarle nada.

- Dada una solicitud seleccionada y el torniquete Cerrado, cuando pulso "Entregar", entonces el torniquete pasa a ModoRegistro y el panel dice "Acerque la tarjeta".
- Dado el modo registro, cuando acerco una tarjeta nueva, entonces queda Entregada a ese cliente, no se mueve el brazo y no se cobra.
- Dada una tarjeta ya registrada a otra persona, cuando la acerco, entonces el panel dice "Tarjeta ya registrada" y puedo probar con otra.
- Dados 60 s sin lectura, cuando vence el tiempo, entonces el torniquete vuelve a Cerrado.

Trazabilidad: RF-09, RF-10 · S6

**HU-18 · Bloquear la tarjeta de un cliente en taquilla** — Must · 1 pt

Como operador, quiero bloquear la tarjeta de un cliente que la reporta perdida en persona, para proteger su saldo aunque no tenga el celular a mano.

- Dado que verifiqué su documento, cuando bloqueo la tarjeta, entonces queda Bloqueada, el saldo no cambia y queda registrado qué operador lo hizo.

Trazabilidad: RF-14 CA4 · RNF-23

**HU-19 · Revisar los intentos de acceso** — Must · 3 pts

Como operador o personal de seguridad, quiero ver cada intento de paso con su resultado y motivo, para entender qué está pasando en el torniquete.

- Dado el panel abierto, cuando ocurre un intento, entonces aparece al inicio de la lista sin recargar.
- (Después del MVP; el MVP muestra el historial sin filtros) Dado el filtro Rechazado y fecha de hoy, cuando lo aplico, entonces solo veo los rechazos de hoy en hora de Colombia, cada uno con su motivo.
- Dado un intento, cuando se muestra, entonces no aparecen el documento ni el UID completos del cliente (RNF-27).

Trazabilidad: RF-23, RF-32 · RNF-27, RNF-42

## Historias del personal de seguridad

Seis historias cubren la vigilancia desde el celular; HU-21 es la que justifica la cámara y YOLO.

**HU-20 · Ver los torniquetes en vivo** — Must · 3 pts

Como personal de seguridad, quiero ver el estado y la conexión de cada torniquete sin recargar, para saber dónde intervenir.

- Dado el panel abierto, cuando un torniquete cambia de estado, entonces lo veo en menos de 2 s.
- Dado un torniquete en Alarma, cuando miro el panel, entonces se distingue por color y muestra desde qué hora está así.
- (Should) Dado un ESP32 sin señal 5 s, cuando vence el plazo, entonces aparece "Fuera de línea" con la hora de la última señal.

Las dos horas llegan en el evento `EstadoTorniquete`: `estadoDesde` y `ultimaSenal` (contratos de integración, C6).

Trazabilidad: RF-31, RF-37, RF-45 CA4 · RNF-05

**HU-21 · Enterarme de un colado al instante** — Must · 8 pts

Como personal de seguridad, quiero que suene la sirena y me llegue una alerta cuando dos personas pasan con una sola validación, para detener al colado.

- Dada una validación y dos cruces de entrada, cuando llega el segundo, entonces suena la sirena, el brazo queda bloqueado y la alerta aparece en mi panel.
- Dado el torniquete Cerrado, cuando alguien cruza sin validar, entonces ocurre lo mismo.
- Dado el torniquete ya en Alarma, cuando cruzan más personas, entonces se suman a la misma alerta.
- Dado el panel en otra vista, cuando se crea la alerta, entonces suena un aviso en el navegador.

Trazabilidad: RF-26, RF-28, RF-33 · RNF-06, RNF-07, RNF-08, RNF-26

**HU-22 · Apagar la alarma desde mi celular** — Must · 2 pts

Como personal de seguridad, quiero apagar la sirena desde el panel, para devolver el torniquete a operación sin ir hasta él.

- Dado un torniquete en Alarma, cuando pulso "Apagar alarma", entonces la sirena se apaga, el torniquete vuelve a Cerrado y la alerta queda Atendida con mi nombre y la hora. En el MVP, apagar la alarma también cierra la alerta; desde la semana 6 queda Atendida hasta que se clasifique (HU-23).
- (Después del MVP; en el MVP un solo rol, Operador, hace taquilla y seguridad) Dado un operador de taquilla, cuando intenta apagarla, entonces no se le permite.

Trazabilidad: RF-34, RF-06 · RNF-04, RNF-23

**HU-23 · Clasificar lo que pasó** — Must · 2 pts

Como personal de seguridad, quiero marcar cada alerta como colado confirmado o falso positivo, para que el equipo mida qué tan bien detecta la cámara.

- Dada una alerta Atendida, cuando la clasifico con una observación, entonces queda Resuelta con esa clasificación.
- Dada una alerta Abierta, cuando intento resolverla, entonces se me pide apagar primero la alarma.

Trazabilidad: RF-35 · RNF-08

**HU-24 · Saber si la cámara dejó de vigilar** — Should · 3 pts

Como personal de seguridad, quiero que el panel me avise cuando el servicio de visión no reporta, para saber que los colados no se están detectando.

- Dados 15 s sin señal de la visión, cuando vence el plazo, entonces el panel muestra "Visión fuera de línea".
- Dada la visión fuera de línea, cuando se valida un paso, entonces el brazo abre, se cierra por expiración y el intento queda "sin verificación de visión".

Trazabilidad: RF-30 · S14

**HU-25 · Ver el video del torniquete** — Could · 5 pts

Como personal de seguridad, quiero ver el video en vivo con las personas detectadas, para confirmar una alerta y calibrar la cámara.

- Dada la visión en línea, cuando abro la vista de video, entonces veo los recuadros sobre las personas y la línea virtual.
- Dado que cierro la vista, cuando se revisa el servidor, entonces no quedó guardado ningún video ni imagen.

Trazabilidad: RF-38 · RNF-26

## Historias del administrador

Cinco historias cubren la configuración; en el MVP solo HU-30 es obligatoria y las demás se cargan como datos semilla o configuración.

**HU-26 · Cambiar la tarifa** — Must · 2 pts

Como administrador, quiero fijar el valor del pasaje desde el panel, para ajustar la tarifa sin tocar código.

- Dada una tarifa de 3.200, cuando la cambio a 3.500, entonces los pasos siguientes cobran 3.500 y los anteriores conservan 3.200.
- Dado un valor de 0 o negativo, cuando intento guardarlo, entonces no se acepta.
- Dado cualquier cambio, cuando se guarda, entonces queda quién lo hizo y desde cuándo rige.

Trazabilidad: RF-41, RF-21 CA3 · RNF-23

**HU-27 · Dar y quitar acceso al personal** — Should · 3 pts

Como administrador, quiero crear, editar y desactivar cuentas de operador y de seguridad, para controlar quién trabaja en el sistema.

- Dado que creo un usuario con rol Seguridad, cuando inicia sesión, entonces puede apagar alarmas.
- Dado un operador desactivado, cuando intenta entrar, entonces no puede, y sus acciones pasadas siguen con su nombre.

Trazabilidad: RF-42, RF-02 CA4

**HU-28 · Registrar un torniquete** — Should · 2 pts

Como administrador, quiero registrar torniquetes con nombre y estación y obtener su credencial, para conectar estaciones nuevas.

- Dado un torniquete nuevo, cuando lo registro, entonces aparece Cerrado en el panel y se genera su credencial.
- Dada una lectura con credencial inválida, cuando llega a la API, entonces se rechaza sin registrar nada.

Trazabilidad: RF-43 · RNF-20

**HU-29 · Ajustar los tiempos de operación** — Could · 3 pts

Como administrador, quiero cambiar la expiración del paso, la duración del modo registro y los montos de recarga, para afinar el sistema durante las pruebas.

- Dada la expiración en 10 s, cuando la cambio a 8 s, entonces los pasos siguientes expiran a los 8 s.
- Dado un valor fuera de rango, cuando intento guardarlo, entonces no se acepta.

Trazabilidad: RF-44 · RNF-40

**HU-30 · Que cada rol haga solo lo suyo** — Must · 3 pts

Como administrador, quiero que cada usuario solo vea y haga lo que permite su rol, y cada cliente solo su propia tarjeta, para proteger los datos y el saldo.

- Dado el cliente A, cuando pide la tarjeta del cliente B, entonces recibe 404.
- Dado un operador de taquilla, cuando intenta cambiar la tarifa, entonces recibe 403.
- Dada una petición sin sesión, cuando pide datos de un cliente, entonces recibe 401.

Trazabilidad: RF-06 · RNF-19

## Historias del torniquete y del equipo

Cinco historias no las pide un usuario final, pero sin ellas la demo falla o el equipo no puede trabajar en paralelo; HU-35 es la primera del proyecto.

**HU-31 · Torniquete seguro sin conexión** — Must · 3 pts

Como operador de la estación, quiero que el torniquete quede cerrado y no cobre cuando no hay conexión con el servidor, para que una falla de red no deje pasar a nadie ni cobre de más.

- Dado el servidor apagado, cuando se acerca una tarjeta, entonces el brazo no abre en 3 s y no hay débito.
- Dado el brazo abierto y 10 s sin comunicación, cuando vence el plazo, entonces el ESP32 cierra el brazo por su cuenta.
- Dado que vuelve la conexión, cuando se acerca una tarjeta válida, entonces el paso funciona sin reiniciar el ESP32.

Trazabilidad: RF-25 · RNF-12, S2, S10

**HU-32 · Recuperarse solo tras un reinicio** — Must · 5 pts

Como operador de la estación, quiero que el torniquete vuelva a su estado correcto después de un corte de energía, para no tener que reconfigurarlo a mano.

- Dado un corte de energía, cuando vuelve, entonces el ESP32 acepta lecturas en 30 s o menos.
- Dado un torniquete en Alarma que se reinicia, cuando hace su primer latido, entonces la sirena vuelve a sonar.
- Dado un estado deseado ya aplicado, cuando el ESP32 lo recibe otra vez, entonces no hace nada.

Trazabilidad: RF-45 · RNF-04, RNF-11, RNF-13, S11

**HU-33 · Cerrar el brazo si nadie cruza** — Must · 3 pts

Como operador de la estación, quiero que el brazo se cierre solo si nadie pasa después de validar, para que no quede abierto para el siguiente.

- Dado un paso autorizado a las 08:00:00, cuando hasta las 08:00:11 no llega ningún cruce, entonces el paso expira y el brazo se cierra.
- Dado un cruce con fotograma de las 08:00:09,5 que llega a las 08:00:10,6, cuando se evalúa, entonces consume el paso y no es fraude.

Trazabilidad: RF-29 · S4, S13

**HU-34 · Probar sin hardware** — Must · 5 pts

Como integrante del equipo, quiero simuladores del ESP32 y de la visión que usen el contrato de la API, para avanzar aunque el hardware no haya llegado.

- Dados solo los dos simuladores y la API, cuando ejecuto el flujo de pasar, colarse y apagar la alarma, entonces funciona completo.
- (Semana 6, cuando exista RF-46) Dado el comando `no-confirmar` en `sim-esp32`, cuando valido un paso, entonces se revierte el débito.

Trazabilidad: RNF-38 · Contratos de integración §11

**HU-35 · Tener contra qué probar desde el primer día** — Must · 3 pts

Como desarrolladora de firmware o de visión, quiero una API esqueleto que responda a lecturas, latidos y cruces con el JSON del contrato, para probar mi parte antes de que exista la lógica real.

- Dado el UID semilla, cuando el ESP32 envía la lectura, entonces recibe `abrir` con `pasoId`, y el latido siguiente devuelve el brazo abierto.
- Dada la API corriendo, cuando abro `/scalar`, entonces veo los endpoints de dispositivos documentados.

La credencial `X-Device-Key` no es parte de esta historia: se verifica en modo tolerante desde la semana 2 y el rechazo con 401 lo cubre HU-28.

Trazabilidad: Contrato API §6 y §7 · RNF-41

## Matriz de trazabilidad

Las 35 historias suman 128 puntos: 90 Must, 30 Should y 8 Could. Cubren los 46 RF v2; la semana y el dueño salen del plan de trabajo v2.1, y entre paréntesis va quien apoya.

| HU | Historia | Épica | RF | Prioridad | Pts | Semana | Dueño |
| --- | --- | --- | --- | --- | --- | --- | --- |
| HU-01 | Crear mi cuenta desde el celular | E1 | RF-01, RF-07 | Must | 5 | 3 | Jeisy (Ana) |
| HU-02 | Entrar y salir de mi cuenta | E1 | RF-02, RF-03 | Must | 3 | 3 | Jeisy (Ana) |
| HU-03 | Recuperar mi contraseña | E1 | RF-04 | Should | 3 | 6 | Jeisy (Ana) |
| HU-04 | Mantener mis datos al día | E1 | RF-05 | Should | 2 | 6 | Jeisy (Ana) |
| HU-05 | Pedir mi tarjeta física por la web | E2 | RF-08 | Must | 2 | 4 | Jeisy (Ana) |
| HU-06 | Activar la tarjeta que recibí | E2 | RF-11, RF-13 | Must | 2 | 4 | Jeisy (Ana) |
| HU-07 | Recargar saldo desde el celular | E3 | RF-15 | Must | 5 | 3–4 | Jeisy (Ana) |
| HU-08 | Ver mi saldo en vivo | E3 | RF-16 | Must | 3 | 3 | Jeisy (Ana, Alex) |
| HU-09 | Pasar el torniquete con mi tarjeta | E4 | RF-18, RF-20 a RF-23, RF-27 | Must | 8 | 2–3 | Alex (Karol) |
| HU-10 | Saber por qué no pude pasar | E4 | RF-24 | Should | 2 | 6 | Alex (Ana) |
| HU-11 | No pagar si el torniquete no abrió | E4 | RF-46 | Must | 5 | 6 | Alex (Karol) |
| HU-12 | Revisar mis viajes | E3 | RF-39, RF-40 | Must | 3 | 4 | Ana (Alex) |
| HU-13 | Revisar mis recargas | E3 | RF-17 | Should | 2 | 6 | Jeisy (Ana) |
| HU-14 | Bloquear mi tarjeta si la pierdo | E2 | RF-14 | Must | 3 | 6 | Jeisy (Ana) |
| HU-15 | Usar mi celular como tarjeta | E2 | RF-12, RF-13, RF-19 | Should | 13 | Opcional | Jeisy (Alex, Karol) |
| HU-16 | Encontrar las solicitudes pendientes | E2 | RF-36 | Must | 3 | 4 | Ana (Jeisy) |
| HU-17 | Entregar una tarjeta con el lector | E2 | RF-09, RF-10 | Must | 5 | 4 | Jeisy (Karol, Ana) |
| HU-18 | Bloquear la tarjeta en taquilla | E2 | RF-14 | Must | 1 | 6 | Jeisy (Ana) |
| HU-19 | Revisar los intentos de acceso | E6 | RF-23, RF-32 | Must | 3 | 4 | Ana (Alex) |
| HU-20 | Ver los torniquetes en vivo | E6 | RF-31, RF-37 | Must | 3 | 2 y 6 | Ana (Alex) |
| HU-21 | Enterarme de un colado al instante | E5 | RF-26, RF-28, RF-33 | Must | 8 | 5 | Alex (Wili, Ana) |
| HU-22 | Apagar la alarma desde mi celular | E6 | RF-34 | Must | 2 | 5 | Ana (Alex) |
| HU-23 | Clasificar lo que pasó | E6 | RF-35 | Must | 2 | 6 | Ana (Alex) |
| HU-24 | Saber si la cámara dejó de vigilar | E5 | RF-30 | Should | 3 | 6 | Alex (Wili) |
| HU-25 | Ver el video del torniquete | E5 | RF-38 | Could | 5 | Si sobra | Wili (Ana) |
| HU-26 | Cambiar la tarifa | E7 | RF-41 | Must | 2 | 6 | Ana (Jeisy) |
| HU-27 | Dar y quitar acceso al personal | E7 | RF-42 | Should | 3 | 6 | Ana (Jeisy) |
| HU-28 | Registrar un torniquete | E7 | RF-43 | Should | 2 | 6 | Ana (Jeisy) |
| HU-29 | Ajustar los tiempos de operación | E7 | RF-44 | Could | 3 | Si sobra | Ana (Jeisy) |
| HU-30 | Que cada rol haga solo lo suyo | E1 | RF-06 | Must | 3 | 3 | Jeisy |
| HU-31 | Torniquete seguro sin conexión | E4 | RF-25 | Must | 3 | 2 | Karol (Alex) |
| HU-32 | Recuperarse solo tras un reinicio | E8 | RF-45 | Must | 5 | 2 | Alex (Karol) |
| HU-33 | Cerrar el brazo si nadie cruza | E4 | RF-29 | Must | 3 | 4 | Alex (Wili) |
| HU-34 | Probar sin hardware | E8 | RNF-38 | Must | 5 | 2 | Karol, Wili |
| HU-35 | Tener contra qué probar desde el primer día | E8 | Contrato API §6–§7 | Must | 3 | 1 | Alex |

HU-15 (13 puntos) se divide antes de empezar, en vincular el celular y validar por desafío-respuesta, y solo entra si se activa el carril opcional del plan.
