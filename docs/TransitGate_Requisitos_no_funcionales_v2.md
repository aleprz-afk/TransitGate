# TransitGate — Requisitos no funcionales (v2)

Sep 26, 2026 · @alex

## 1. Alcance y convenciones

Este documento fija 42 requisitos no funcionales (RNF) de TransitGate, cada uno con un criterio medible: 23 Must y 19 Should. Complementa los 46 RF del documento de requisitos funcionales (v2) y usa sus supuestos (S1 a S15).

Según el enfoque visto en clase, un RNF responde al cómo: define un criterio de aceptación que QA puede comprobar con una prueba concreta.

### Plantilla

Cada RNF tiene: categoría, prioridad (MoSCoW, con el mismo significado que en los RF), RF relacionados, descripción, criterio de aceptación con número y unidad, y método de verificación.

### Entorno de referencia

Todos los criterios se miden en este entorno, que es el de la demo:

| Elemento | Valor |
| --- | --- |
| Servidor | Laptop del estudiante con la API, la base de datos y el servicio de visión en la misma máquina (un solo reloj) |
| Red | WiFi dedicada de 2,4 GHz (router o hotspot) con WPA2-Personal; laptop con IP fija |
| Estación | Un torniquete: ESP32 principal + PN532 + servo + sirena; ESP32-CAM en vista cenital |
| Clientes | Celular Android con NFC; navegador de celular y de computador |
| Iluminación | Al menos 200 lux en la zona del torniquete, medidos con una app de luxómetro |

### Cómo se mide

- **p95:** el percentil 95 de al menos 50 mediciones; es decir, 95 de cada 100 pasadas cumplen el límite.
- **Tiempos:** marcas de tiempo en los logs del ESP32 y del servidor, con el reloj del ESP32 sincronizado por NTP. Cuando interviene un movimiento físico (el brazo), se graba con un celular a 60 fps y se cuentan fotogramas.
- **Registro:** cada corrida de prueba se anota en una hoja de pruebas con fecha, condiciones y resultado, para usarla como evidencia en la Fase 5.

## 2. Rendimiento

Seis RNF (RNF-01 a RNF-06) fijan los tiempos de respuesta. El central es RNF-01: el brazo empieza a abrir antes de 1 s desde que se acerca la tarjeta; los demás reparten ese presupuesto o cubren los caminos en vivo.

### RNF-01 — Respuesta del torniquete con tarjeta física

**Categoría:** Rendimiento · **Prioridad:** Must · **Relacionado con:** RF-18, RF-20, RF-21, RF-22

**Descripción:** El torniquete debe abrir el brazo poco después de que el cliente acerca una tarjeta válida.

**Criterio de aceptación:** p95 ≤ 1,0 s desde que la tarjeta toca el lector hasta que el servo empieza a moverse, en 50 pasadas. Ambos momentos se ven en el video.

**Verificación:** video a 60 fps de 50 pasadas; se cuentan los fotogramas entre el contacto de la tarjeta y el primer movimiento del brazo.

Presupuesto de tiempo propuesto para cumplirlo:

| Tramo | Presupuesto (ms) |
| --- | --- |
| Detección y lectura del UID en el PN532 | 150 |
| Envío por WiFi y respuesta (ida y vuelta) | 300 |
| Procesamiento en el servidor (RNF-03) | 200 |
| Arranque del movimiento del servo | 100 |
| Margen | 250 |
| **Total** | **1.000** |

**Nota para el firmware:** el presupuesto solo se cumple si la consulta de estado (cada 300 ms) no bloquea la lectura del PN532. `urequests` bloquea incluso dentro de `asyncio`, así que hay dos caminos: un cliente HTTP asíncrono (el `aiohttp` de micropython-lib o sockets con `asyncio.open_connection`), o un timeout corto de unos 250 ms en la consulta de estado. Se elige en la Fase 1.

### RNF-02 — Respuesta del torniquete con celular

**Categoría:** Rendimiento · **Prioridad:** Should · **Relacionado con:** RF-19

**Descripción:** El torniquete debe abrir el brazo con el celular casi tan rápido como con la tarjeta física, aunque el intercambio NFC sea más largo.

**Criterio de aceptación:** p95 ≤ 1,5 s desde que el celular entra en el campo del lector hasta que el servo empieza a moverse, en 50 pasadas.

**Verificación:** el mismo método de RNF-01, con el celular vinculado.

### RNF-03 — Procesamiento de la validación en el servidor

**Categoría:** Rendimiento · **Prioridad:** Must · **Relacionado con:** RF-20, RF-21, RF-23

**Descripción:** La API debe decidir un paso (validar, debitar y registrar) rápido, incluso con la base de datos cargada.

**Criterio de aceptación:** p95 ≤ 200 ms entre la llegada de la lectura a la API y el envío de la respuesta, con 1.000 tarjetas y 10.000 intentos cargados en la base de datos.

**Verificación:** un middleware registra la duración de cada petición; un script envía 200 lecturas simuladas sobre la base con datos de prueba.

### RNF-04 — Latencia del estado deseado

**Categoría:** Rendimiento · **Prioridad:** Must · **Relacionado con:** RF-27, RF-28, RF-34, RF-45

**Descripción:** Los cambios del estado deseado del torniquete (cerrar el brazo, encender o apagar la sirena) deben aplicarse casi de inmediato (RF-45).

**Criterio de aceptación:** p95 ≤ 500 ms desde que el servidor cambia el estado deseado hasta que el ESP32 lo aplica. Se mide hasta la ejecución y no hasta la confirmación, que llega en la consulta siguiente.

**Verificación:** el servidor registra la hora de cada cambio y el ESP32, con su reloj sincronizado por NTP, la hora en que lo aplica; se comparan 50 cambios.

### RNF-05 — Actualización en vivo de la web

**Categoría:** Rendimiento · **Prioridad:** Must · **Relacionado con:** RF-16, RF-24, RF-31, RF-33

**Descripción:** El portal y el panel deben reflejar los eventos sin que el usuario recargue la página.

**Criterio de aceptación:** p95 ≤ 2 s entre un evento en el servidor (débito, recarga, alerta o cambio de estado del torniquete) y su aparición en pantalla.

**Verificación:** con el navegador abierto en la misma laptop (mismo reloj), la web registra la hora de llegada de cada evento y se compara con la hora del evento en el servidor, en 50 eventos.

### RNF-06 — Latencia del reporte de cruce

**Categoría:** Rendimiento · **Prioridad:** Must · **Relacionado con:** RF-26, RF-29

**Descripción:** El servicio de visión debe reportar cada cruce con poco retraso respecto al momento en que ocurrió.

**Criterio de aceptación:** p95 ≤ 1 s de retraso completo, desde la captura real del fotograma hasta la llegada del reporte a la API. La gracia de S13 no sale del p95: se fija con el máximo de al menos 100 mediciones más un margen del 50 %, porque un reporte tardío que caiga fuera de la gracia produce una falsa alarma.

**Verificación:** el stream MJPEG no trae la hora de captura, así que la cámara filma un cronómetro con milisegundos en la pantalla de la laptop; la hora visible en el fotograma se compara con la hora de llegada de ese fotograma y con la del reporte en la API. Se hacen al menos 100 cruces en la Fase 1.

## 3. Precisión de la visión

Cuatro RNF (RNF-07 a RNF-10) miden qué tan bien la cámara y YOLO detectan colados sin molestar a los usuarios legítimos. Los umbrales son metas iniciales y se confirman con la prueba de YOLO de la Fase 1 (sección 10).

### RNF-07 — Detección de colados

**Categoría:** Precisión · **Prioridad:** Must · **Relacionado con:** RF-26, RF-28

**Descripción:** El sistema debe detectar la gran mayoría de los casos en que dos personas pasan con una sola validación.

**Criterio de aceptación:** al menos 18 de 20 intentos de colado generan alerta (≥ 90 %).

**Verificación:** protocolo de 20 intentos tras una sola validación, con tres variantes: una persona pegada detrás de otra, dos lado a lado y una con mochila grande. Iluminación de al menos 200 lux.

### RNF-08 — Falsas alarmas

**Categoría:** Precisión · **Prioridad:** Must · **Relacionado con:** RF-28, RF-35

**Descripción:** El sistema no debe activar la alarma cuando una persona pasa correctamente.

**Criterio de aceptación:** como máximo 2 alarmas en 40 pasos legítimos individuales (≤ 5 %).

**Verificación:** protocolo de 40 pasos con una persona por validación, que incluye personas caminando cerca sin cruzar y cruces en sentido de salida. Cada alerta se clasifica con RF-35 y se cuentan los falsos positivos.

### RNF-09 — Conteo de cruces

**Categoría:** Precisión · **Prioridad:** Should · **Relacionado con:** RF-26, RF-27

**Descripción:** Cada persona que cruza debe contarse exactamente una vez y en el sentido correcto.

**Criterio de aceptación:** al menos 48 de 50 cruces individuales se cuentan una sola vez y con el sentido correcto (≥ 96 %).

**Verificación:** 50 cruces (40 de entrada y 10 de salida) comparados con el registro de `Cruce`.

### RNF-10 — Tasa de procesamiento del video

**Categoría:** Precisión · **Prioridad:** Should · **Relacionado con:** RF-26

**Descripción:** El servicio de visión debe procesar suficientes fotogramas para seguir a cada persona. Una persona caminando cruza el campo de la cámara en cerca de 1 s, y el tracking necesita varios fotogramas por persona.

**Criterio de aceptación:** al menos 10 fotogramas procesados por segundo, sostenidos durante 30 min, con YOLO nano y una entrada de 320 px (416 px si la laptop tiene GPU).

**Verificación:** el servicio de visión registra los fotogramas procesados por segundo; se revisa el mínimo de la sesión.

## 4. Disponibilidad y fiabilidad

Siete RNF (RNF-11 a RNF-16 y RNF-42) aseguran que el sistema se recupera solo de cortes y que el saldo nunca queda mal. RNF-14 es, en la práctica, el ensayo general de la demo.

### RNF-11 — Recuperación del ESP32 tras un reinicio

**Categoría:** Disponibilidad · **Prioridad:** Must · **Relacionado con:** RF-25, RF-45

**Descripción:** Tras un corte de energía, el ESP32 debe volver a operar sin intervención. Arranca con el brazo cerrado y, al reconectar, recibe del servidor el estado que debe tener el torniquete.

**Criterio de aceptación:** ≤ 30 s desde que vuelve la energía hasta que acepta lecturas. Si el torniquete estaba en `Alarma`, la sirena vuelve a sonar tras reconectar, porque recibe el estado deseado (RF-45, CA3).

**Verificación:** 10 cortes de energía, dos de ellos con el torniquete en `Alarma`.

### RNF-12 — Reconexión automática al WiFi

**Categoría:** Disponibilidad · **Prioridad:** Must · **Relacionado con:** RF-25

**Descripción:** Si la red WiFi se cae, el ESP32 y el ESP32-CAM deben reconectarse solos cuando vuelva.

**Criterio de aceptación:** ≤ 15 s desde que la red vuelve hasta que ambos dispositivos operan de nuevo, sin reiniciarlos.

**Verificación:** 10 apagados del router de 30 s cada uno.

### RNF-13 — Recuperación del estado tras reiniciar el servidor

**Categoría:** Disponibilidad · **Prioridad:** Should · **Relacionado con:** RF-22, RF-28, RF-29

**Descripción:** Si la API se reinicia, debe retomar el estado desde la base de datos y no desde la memoria.

**Criterio de aceptación:** ≤ 60 s hasta volver a atender. Los torniquetes conservan su estado, los pasos `Pendiente` ya vencidos quedan `Expirado` y las alertas `Abierta` siguen abiertas.

**Verificación:** reiniciar la API con un torniquete en `EsperandoCruce` y otra vez con uno en `Alarma`.

### RNF-14 — Operación continua

**Categoría:** Disponibilidad · **Prioridad:** Must · **Relacionado con:** todos los Must

**Descripción:** El sistema completo debe funcionar durante una sesión de demo sin que nadie lo reinicie.

**Criterio de aceptación:** una sesión de 30 min con al menos 50 pasos, 5 colados simulados y 10 recargas termina con 0 reinicios manuales y 0 errores 500 en la API.

**Verificación:** ensayo de la demo en la Fase 5, con el log de la API como evidencia.

### RNF-15 — Atomicidad del paso

**Categoría:** Fiabilidad · **Prioridad:** Must · **Relacionado con:** RF-21, RF-22, RF-23

**Descripción:** El débito, el `IntentoAcceso` y el `PasoAutorizado` deben guardarse en una sola transacción: o quedan los tres o ninguno.

**Criterio de aceptación:** si el proceso falla a mitad de la validación, el saldo no cambia y no queda ningún intento ni paso a medias.

**Verificación:** prueba automatizada que fuerza una excepción después del débito y comprueba el saldo y los registros.

### RNF-16 — Consistencia del saldo con operaciones simultáneas

**Categoría:** Fiabilidad · **Prioridad:** Must · **Relacionado con:** RF-15, RF-21, RF-46

**Descripción:** Dos operaciones al mismo tiempo sobre la misma tarjeta no deben pisarse.

**Criterio de aceptación:** 20 recargas de 1.000 enviadas en paralelo aumentan el saldo exactamente en 20.000; una recarga y un débito simultáneos dejan el saldo correcto.

**Verificación:** prueba de integración con peticiones paralelas sobre la misma tarjeta. En EF Core se resuelve con una columna de versión (control de concurrencia optimista) o con un `UPDATE` atómico sobre el saldo.

### RNF-42 — Fechas en UTC y hora de Colombia

**Categoría:** Fiabilidad · **Prioridad:** Should · **Relacionado con:** RF-32, RF-39, RF-40

**Descripción:** Todas las fechas deben guardarse en UTC y mostrarse en hora de Colombia (UTC−5, sin horario de verano). Sin esto, filtros como "rechazos de hoy" fallan desde las 7 p. m., cuando en UTC ya es el día siguiente. Se agregó en la revisión y se numera al final.

**Criterio de aceptación:** un intento registrado a las 8:30 p. m. hora de Colombia aparece en el filtro "hoy" de ese día y se muestra con esa hora.

**Verificación:** prueba automatizada con intentos simulados a las 7:30 p. m. y a las 11:59 p. m. hora de Colombia.

## 5. Seguridad

Nueve RNF (RNF-17 a RNF-25) protegen cuentas, saldo y dispositivos. Las limitaciones conocidas del prototipo (UID de MIFARE clonable, iPhone sin emulación) siguen documentadas en la arquitectura v2 y no se repiten aquí.

### RNF-17 — Contraseñas con hash y sal

**Categoría:** Seguridad · **Prioridad:** Must · **Relacionado con:** RF-01, RF-04, RF-05

**Descripción:** Las contraseñas deben guardarse con un algoritmo lento y con sal, por ejemplo el `PasswordHasher` de ASP.NET Core Identity (PBKDF2).

**Criterio de aceptación:** ninguna contraseña está en texto plano ni con un hash rápido sin sal (MD5, SHA-1); dos cuentas con la misma contraseña tienen hashes distintos.

**Verificación:** crear dos cuentas con la misma contraseña y revisar la tabla de usuarios.

### RNF-18 — Sesiones con expiración

**Categoría:** Seguridad · **Prioridad:** Should · **Relacionado con:** RF-02, RF-03

**Descripción:** Las sesiones deben vencer solas y dejar de servir al cerrarse. Un JWT sigue siendo válido hasta que vence aunque el usuario cierre sesión, así que la web usa la cookie de autenticación de ASP.NET Core Identity, que sí se invalida. La app Android usa un token de vida corta solo para iniciar sesión y vincularse; la emulación NFC no depende de la sesión (RF-19, CA6).

**Criterio de aceptación:** la sesión web vence tras 60 min de inactividad o menos; tras vencer o cerrar sesión, toda petición con esa sesión recibe 401; el token de la app vence en 15 min o menos.

**Verificación:** prueba automatizada con la duración del token reducida en la configuración de pruebas.

### RNF-19 — Autorización en el servidor

**Categoría:** Seguridad · **Prioridad:** Must · **Relacionado con:** RF-06

**Descripción:** Los permisos de RF-06 deben comprobarse en la API; ocultar un botón en la web no cuenta como control.

**Criterio de aceptación:** en el 100 % de los endpoints, una petición sin sesión recibe 401, una con sesión pero sin permiso de rol recibe 403, y una sobre recursos de otro cliente recibe 404, para no revelar que existen (RF-06).

**Verificación:** prueba automatizada que recorre cada endpoint con el token del cliente A sobre recursos del cliente B, y con un token de cliente sobre los endpoints del panel.

### RNF-20 — Canales y credenciales de los dispositivos

**Categoría:** Seguridad · **Prioridad:** Should · **Relacionado con:** RF-43

**Descripción:** La web y la app deben usar HTTPS. El ESP32 y el servicio de visión pueden usar HTTP, pero solo dentro de la red de la demo y con la credencial de su torniquete.

**Criterio de aceptación:** toda lectura, consulta de órdenes o cruce sin credencial válida recibe 401; si se usa un túnel hacia internet, solo expone los endpoints de la web y la app.

**Verificación:** peticiones con `curl` sin credencial y con credencial falsa; revisión de las rutas publicadas por el túnel.

### RNF-21 — Protección de la clave de vinculación

**Categoría:** Seguridad · **Prioridad:** Should · **Relacionado con:** RF-12, RF-19

**Descripción:** La clave del celular debe ser impredecible y estar protegida en ambos extremos. El servidor la necesita para verificar el HMAC, así que no puede guardarla como hash: debe cifrarla.

**Criterio de aceptación:** clave de al menos 256 bits generada con un generador criptográfico; cifrada en la base de datos (Data Protection de ASP.NET Core); guardada en Android Keystore en el celular; nunca aparece en logs.

**Verificación:** revisar que la columna de la base de datos no es legible y buscar la clave en los logs tras 10 vinculaciones.

### RNF-22 — Límite de intentos de inicio de sesión

**Categoría:** Seguridad · **Prioridad:** Should · **Relacionado con:** RF-02

**Descripción:** El sistema debe frenar los intentos de adivinar contraseñas.

**Criterio de aceptación:** 5 intentos fallidos seguidos bloquean la cuenta durante 5 min.

**Verificación:** prueba automatizada con 6 intentos fallidos y uno correcto dentro de los 5 min.

### RNF-23 — Registro de auditoría

**Categoría:** Seguridad · **Prioridad:** Should · **Relacionado con:** RF-10, RF-14, RF-34, RF-35, RF-41, RF-42

**Descripción:** Las acciones sensibles del personal deben quedar registradas.

**Criterio de aceptación:** entregar tarjetas, bloquear soportes, apagar alarmas, resolver alertas, cambiar la tarifa y gestionar operadores quedan con usuario, fecha y hora; el registro no se puede editar ni borrar desde la web.

**Verificación:** hacer cada acción una vez y consultar el registro.

### RNF-24 — Validación de entradas en la API

**Categoría:** Seguridad · **Prioridad:** Should · **Relacionado con:** todos los RF con entrada de datos

**Descripción:** La API debe rechazar datos inválidos de forma controlada. El dinero se maneja como entero en pesos, nunca como número de punto flotante.

**Criterio de aceptación:** JSON mal formado, campos faltantes o valores fuera de rango reciben 400 con un mensaje claro, sin errores 500.

**Verificación:** un script envía 20 cuerpos inválidos a los endpoints principales.

### RNF-25 — Secretos fuera del repositorio

**Categoría:** Seguridad · **Prioridad:** Must · **Relacionado con:** RF-43

**Descripción:** Contraseñas de WiFi, cadenas de conexión, credenciales de torniquetes y claves de firma deben cargarse desde archivos locales ignorados por git: user-secrets de .NET en el servidor y un `config.json` en el ESP32.

**Criterio de aceptación:** ningún secreto aparece en el historial de git del monorepo.

**Verificación:** escaneo del repositorio con gitleaks antes de cada entrega.

## 6. Privacidad

Cuatro RNF (RNF-26 a RNF-29) aterrizan la decisión 8 de la guía (Ley 1581 de 2012): no se guardan imágenes, se piden pocos datos y el cliente autoriza su tratamiento.

### RNF-26 — No almacenar imágenes ni video

**Categoría:** Privacidad · **Prioridad:** Must · **Relacionado con:** RF-26, RF-38

**Descripción:** El servicio de visión debe procesar los fotogramas solo en memoria. Ultralytics guarda resultados en la carpeta `runs/` si se activa `save=True`, así que esa opción debe quedar desactivada.

**Criterio de aceptación:** tras una sesión de 30 min hay 0 archivos de imagen o video en las carpetas del sistema, y ninguna tabla de la base de datos tiene columnas para imágenes.

**Verificación:** búsqueda de archivos `.jpg`, `.png`, `.mp4` y `.avi` en `server/` y `vision/` después de la sesión, y revisión del esquema.

### RNF-27 — Minimización y enmascaramiento de datos

**Categoría:** Privacidad · **Prioridad:** Must · **Relacionado con:** RF-01, RF-32

**Descripción:** El sistema debe pedir solo los datos que usa y mostrar lo mínimo al personal.

**Criterio de aceptación:** el registro pide solo nombre, tipo y número de documento, correo, teléfono y contraseña; el panel muestra solo los 4 últimos dígitos del documento, salvo en la búsqueda de taquilla; los logs no contienen documentos ni correos completos.

**Verificación:** revisión del formulario, del panel y de los logs de una sesión de prueba.

### RNF-28 — Autorización del tratamiento de datos

**Categoría:** Privacidad · **Prioridad:** Must · **Relacionado con:** RF-01

**Descripción:** El cliente debe autorizar el tratamiento de sus datos antes de crear la cuenta, como exige el artículo 9 de la Ley 1581 de 2012.

**Criterio de aceptación:** el registro no se completa sin marcar la aceptación de la política de tratamiento (casilla desmarcada por defecto); se guardan la fecha y la versión aceptada; la política se puede leer desde el portal.

**Verificación:** intentar registrarse sin marcar la casilla y revisar el registro de un cliente creado.

### RNF-29 — Aviso de videovigilancia

**Categoría:** Privacidad · **Prioridad:** Should · **Relacionado con:** RF-26

**Descripción:** Las personas que se acercan al torniquete deben saber que hay una cámara. Procesar imágenes de personas ya es tratamiento de datos aunque no se guarden, y la guía de videovigilancia de la Superintendencia de Industria y Comercio pide avisos visibles.

**Criterio de aceptación:** un letrero visible junto al torniquete informa que hay cámara, que no se graban imágenes y quién responde por el sistema.

**Verificación:** inspección del montaje de la demo.

## 7. Usabilidad

Tres RNF (RNF-30 a RNF-32) miden la promesa del proyecto: resolver desde el celular lo que antes exigía hacer fila en la taquilla.

### RNF-30 — Registro y primera recarga en 3 min

**Categoría:** Usabilidad · **Prioridad:** Should · **Relacionado con:** RF-01, RF-15

**Descripción:** Un usuario nuevo debe poder crear su cuenta y recargar sin ayuda.

**Criterio de aceptación:** al menos 4 de 5 personas que no conocen el sistema completan registro y primera recarga en 3 min o menos, desde su propio celular.

**Verificación:** prueba cronometrada con 5 compañeros ajenos al proyecto, sin explicación previa.

### RNF-31 — Portal usable en celular

**Categoría:** Usabilidad · **Prioridad:** Must · **Relacionado con:** RF-01 a RF-17, RF-39

**Descripción:** El portal del cliente debe estar diseñado primero para pantallas de celular.

**Criterio de aceptación:** en pantallas de 360 px de ancho ninguna página del portal tiene desplazamiento horizontal, y los botones miden al menos 44 × 44 px.

**Verificación:** modo de dispositivo de Chrome DevTools a 360 × 800 y prueba en un celular real.

### RNF-32 — Mensajes en español claro

**Categoría:** Usabilidad · **Prioridad:** Should · **Relacionado con:** RF-24, RF-39

**Descripción:** El usuario debe entender por qué algo falló sin ver códigos internos.

**Criterio de aceptación:** el 100 % de los motivos del catálogo y de los errores de la API tienen un texto en español para el usuario, sin códigos ni trazas; toda la interfaz está en español.

**Verificación:** provocar cada motivo del catálogo de los RF y revisar el texto que aparece en el portal.

## 8. Compatibilidad

Cinco RNF (RNF-33 a RNF-37) fijan con qué navegadores, celulares, tarjetas, red y cámara debe funcionar el sistema.

### RNF-33 — Navegadores soportados

**Categoría:** Compatibilidad · **Prioridad:** Must · **Relacionado con:** RF-01 a RF-17, RF-31 a RF-39

**Descripción:** El portal y el panel deben funcionar en los navegadores que usan los clientes, incluidos los de iPhone, que solo usan la web y la tarjeta física.

**Criterio de aceptación:** todos los flujos Must funcionan en Chrome para Android, Safari para iOS y Chrome o Edge de escritorio, en sus dos últimas versiones mayores.

**Verificación:** recorrer los flujos Must en cada navegador.

### RNF-34 — Versiones de Android para la app

**Categoría:** Compatibilidad · **Prioridad:** Should · **Relacionado con:** RF-12, RF-19

**Descripción:** La app debe funcionar en la mayoría de celulares Android con NFC. Algunos equipos exigen el celular desbloqueado para emular tarjetas; si pasa, se documenta.

**Criterio de aceptación:** la app funciona en Android 8.0 (API 26) o superior con NFC y HCE, con la pantalla encendida; en celulares sin HCE informa la incompatibilidad (RF-12, CA4).

**Verificación:** prueba en al menos 2 celulares de marcas distintas.

### RNF-35 — Tipos de tarjeta NFC

**Categoría:** Compatibilidad · **Prioridad:** Must · **Relacionado con:** RF-10, RF-18

**Descripción:** El lector debe aceptar los tipos de tarjeta fáciles de conseguir, que tienen UID de distinta longitud.

**Criterio de aceptación:** se aceptan MIFARE Classic 1K (UID de 4 bytes) y NTAG213/215 (UID de 7 bytes); el UID se envía siempre como texto hexadecimal en mayúsculas con su longitud real. Un celular con la app se identifica por SELECT AID, no por su UID aleatorio (RF-18, CA5).

**Verificación:** registrar y usar al menos una tarjeta de cada tipo.

### RNF-36 — Red WiFi de la demo

**Categoría:** Compatibilidad · **Prioridad:** Must · **Relacionado con:** RF-18, RF-26, RF-45

**Descripción:** El sistema debe usar una red propia. Las redes universitarias suelen ser WPA2-Enterprise, que el ESP32 con MicroPython no maneja con facilidad, y a menudo aíslan a los dispositivos entre sí, lo que impide que el ESP32 llegue a la laptop.

**Criterio de aceptación:** la demo funciona en una red de 2,4 GHz con WPA2-Personal (router o hotspot) sin depender de la red de la universidad; la laptop mantiene su IP mediante reserva DHCP o IP estática.

**Verificación:** ensayo completo en el salón de la demo.

### RNF-37 — Estabilidad del stream de la cámara

**Categoría:** Compatibilidad · **Prioridad:** Should · **Relacionado con:** RF-26, RF-30

**Descripción:** El ESP32-CAM (sensor OV3660) debe transmitir video estable. Con una alimentación débil suele reiniciarse por caídas de voltaje.

**Criterio de aceptación:** transmite MJPEG a la resolución elegida en la Fase 1 con al menos 15 fps durante 30 min sin reiniciarse, alimentado con una fuente de 5 V de al menos 2 A. Los 15 fps dejan margen sobre los 10 fps que exige el procesamiento (RNF-10).

**Verificación:** sesión de 30 min con registro de fps en el servicio de visión y del contador de reinicios del ESP32-CAM.

## 9. Mantenibilidad y pruebas

Cuatro RNF (RNF-38 a RNF-41) permiten que los dos frentes trabajen en paralelo y que los criterios se prueben de forma repetible. RNF-38 es el que hace posible la división del trabajo del plan.

### RNF-38 — Simuladores de dispositivos

**Categoría:** Mantenibilidad · **Prioridad:** Must · **Relacionado con:** RF-18, RF-19, RF-26, RF-45

**Descripción:** Deben existir dos simuladores que usen exactamente el contrato de la API: uno del ESP32 (lecturas, consulta de órdenes, desafíos) y uno del servicio de visión (cruces).

**Criterio de aceptación:** con solo los simuladores se ejecuta completo el flujo de pasar, colarse y apagar la alarma, sin ningún hardware.

**Verificación:** demostración del flujo con los simuladores al cerrar la Fase 1.

### RNF-39 — Pruebas automatizadas del dominio

**Categoría:** Mantenibilidad · **Prioridad:** Should · **Relacionado con:** todos los RF Must del servidor

**Descripción:** Las reglas del servidor deben tener pruebas automatizadas para detectar regresiones.

**Criterio de aceptación:** cada criterio de aceptación de los RF Must del servidor tiene al menos una prueba en xUnit, y la cobertura de `Domain` y `Application` es de al menos 70 %.

**Verificación:** reporte de cobertura con coverlet en cada entrega.

### RNF-40 — Configuración fuera del código

**Categoría:** Mantenibilidad · **Prioridad:** Should · **Relacionado con:** RF-44

**Descripción:** Los valores que se ajustan durante las pruebas deben cambiarse sin recompilar el servidor ni volver a flashear el firmware. Los archivos dan los valores por defecto; si el administrador cambia uno desde el panel (RF-44), el valor guardado en la base de datos prevalece.

**Criterio de aceptación:** IP y puerto del servidor, credenciales, los tiempos de S4, S6, S10, S11, S13 y S14 y los montos de S8 están en archivos de configuración (`appsettings` y `config.json` del ESP32).

**Verificación:** cambiar la expiración de 10 s a 8 s solo por configuración y comprobarlo en el torniquete.

### RNF-41 — Documentación de la API

**Categoría:** Mantenibilidad · **Prioridad:** Should · **Relacionado con:** todos los RF

**Descripción:** La API debe documentarse sola con OpenAPI y Scalar, como recuerda la guía para .NET 10.

**Criterio de aceptación:** todos los endpoints aparecen en OpenAPI con ejemplos de petición y respuesta, y coinciden con el documento del contrato de la API.

**Verificación:** revisión cruzada entre Scalar y el contrato antes de cada integración entre frentes.

## 10. Puntos abiertos

Tras la revisión quedan cuatro puntos abiertos; los dos primeros conviene cerrarlos antes de la Fase 1. Ya se resolvieron el sensor de la cámara (OV3660, se corrige en la arquitectura) y el criterio nuevo de RF-01 que pide RNF-28 (aplicado en los RF v2).

1. **Red propia para la demo.** RNF-36 exige un router o un hotspot de 2,4 GHz. Hay que decidir cuál, porque sus datos van en el `config.json` del firmware desde el primer día.
2. **HTTPS y despliegue.** Los celulares no confían en el certificado de desarrollo de .NET, así que HTTPS dentro de la red local es incómodo. Opción A: un túnel (Cloudflare Tunnel o ngrok) da HTTPS a la web y la app, y los dispositivos siguen por HTTP en la red local. Opción B: todo por HTTP en la red local, documentado como limitación. La opción A también facilita la cookie segura de RNF-18 en los celulares (RNF-20).
3. **Umbrales de visión.** El 90 % de detección y el 5 % de falsas alarmas (RNF-07, RNF-08) son metas iniciales. Si la prueba de la Fase 1 no las alcanza, se ajusta en este orden: iluminación, altura y ángulo de la cámara, resolución y, por último, afinar YOLO con imágenes propias.
4. **Personas para las pruebas.** RNF-07, RNF-08 y RNF-30 necesitan varias personas (20 colados, 40 pasos, 5 usuarios nuevos). Conviene agendar una sesión preliminar en la Fase 3 y la definitiva en la Fase 5.

## 11. Matriz resumen y plan de QA

La columna Fase indica cuándo se puede medir cada RNF por primera vez; todos se vuelven a medir en la Fase 5 como evidencia final. La fase 0 significa "desde el inicio del repositorio".

| RNF | Nombre | Categoría | Prioridad | Verificación | Fase |
| --- | --- | --- | --- | --- | --- |
| RNF-01 | Respuesta con tarjeta física | Rendimiento | Must | Video a 60 fps, 50 pasadas | 1 |
| RNF-02 | Respuesta con celular | Rendimiento | Should | Video a 60 fps, 50 pasadas | 4 |
| RNF-03 | Procesamiento en el servidor | Rendimiento | Must | Middleware de tiempos, 200 lecturas | 2 |
| RNF-04 | Latencia del estado deseado | Rendimiento | Must | Logs del servidor y del ESP32 con NTP, 50 cambios | 1 |
| RNF-05 | Actualización en vivo de la web | Rendimiento | Must | Marcas de tiempo, 50 eventos | 2 |
| RNF-06 | Latencia del reporte de cruce | Rendimiento | Must | Cronómetro filmado, 100 cruces | 1 |
| RNF-07 | Detección de colados | Precisión | Must | Protocolo de 20 colados | 3 |
| RNF-08 | Falsas alarmas | Precisión | Must | Protocolo de 40 pasos | 3 |
| RNF-09 | Conteo de cruces | Precisión | Should | 50 cruces contra registro | 3 |
| RNF-10 | Tasa de procesamiento del video | Precisión | Should | Log de fps, 30 min | 1 |
| RNF-11 | Recuperación del ESP32 | Disponibilidad | Must | 10 cortes de energía | 1 |
| RNF-12 | Reconexión al WiFi | Disponibilidad | Must | 10 apagados del router | 1 |
| RNF-13 | Recuperación del servidor | Disponibilidad | Should | Reinicios en `EsperandoCruce` y `Alarma` | 2 |
| RNF-14 | Operación continua | Disponibilidad | Must | Ensayo de demo de 30 min | 5 |
| RNF-15 | Atomicidad del paso | Fiabilidad | Must | Prueba con fallo forzado | 2 |
| RNF-16 | Saldo con operaciones simultáneas | Fiabilidad | Must | 20 recargas en paralelo | 2 |
| RNF-17 | Contraseñas con hash y sal | Seguridad | Must | Revisión de la tabla de usuarios | 2 |
| RNF-18 | Sesiones con expiración | Seguridad | Should | Petición tras cerrar sesión | 2 |
| RNF-19 | Autorización en el servidor | Seguridad | Must | Recorrido de endpoints: 401, 403 y 404 | 2 |
| RNF-20 | Canales y credenciales de dispositivos | Seguridad | Should | `curl` sin credencial | 2 |
| RNF-21 | Protección de la clave del celular | Seguridad | Should | Revisión de BD y logs | 4 |
| RNF-22 | Límite de intentos de sesión | Seguridad | Should | 6 intentos fallidos | 2 |
| RNF-23 | Registro de auditoría | Seguridad | Should | Cada acción sensible una vez | 2 |
| RNF-24 | Validación de entradas | Seguridad | Should | 20 cuerpos inválidos | 2 |
| RNF-25 | Secretos fuera del repositorio | Seguridad | Must | Escaneo con gitleaks | 0 |
| RNF-26 | Sin imágenes ni video | Privacidad | Must | Búsqueda de archivos tras 30 min | 3 |
| RNF-27 | Minimización y enmascaramiento | Privacidad | Must | Revisión de formulario, panel y logs | 2 |
| RNF-28 | Autorización del tratamiento | Privacidad | Must | Registro sin marcar la casilla | 2 |
| RNF-29 | Aviso de videovigilancia | Privacidad | Should | Inspección del montaje | 5 |
| RNF-30 | Registro y recarga en 3 min | Usabilidad | Should | Prueba con 5 usuarios | 5 |
| RNF-31 | Portal usable en celular | Usabilidad | Must | DevTools a 360 px y celular real | 2 |
| RNF-32 | Mensajes en español claro | Usabilidad | Should | Provocar cada motivo | 2 |
| RNF-33 | Navegadores soportados | Compatibilidad | Must | Flujos Must en cada navegador | 2 |
| RNF-34 | Versiones de Android | Compatibilidad | Should | 2 celulares de marcas distintas | 4 |
| RNF-35 | Tipos de tarjeta NFC | Compatibilidad | Must | Una tarjeta de cada tipo y un celular | 1 |
| RNF-36 | Red WiFi de la demo | Compatibilidad | Must | Ensayo en el salón | 0 |
| RNF-37 | Estabilidad del stream | Compatibilidad | Should | 30 min con registro de fps | 1 |
| RNF-38 | Simuladores de dispositivos | Mantenibilidad | Must | Flujo completo sin hardware | 1 |
| RNF-39 | Pruebas automatizadas | Mantenibilidad | Should | Reporte de cobertura | 2 |
| RNF-40 | Configuración fuera del código | Mantenibilidad | Should | Cambiar la expiración por configuración | 1 |
| RNF-41 | Documentación de la API | Mantenibilidad | Should | Scalar contra el contrato | 1 |
| RNF-42 | Fechas en UTC y hora de Colombia | Fiabilidad | Should | Intentos simulados de noche | 2 |
