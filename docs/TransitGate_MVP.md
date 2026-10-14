# TransitGate — Producto mínimo viable (MVP) (v1.2)

**Cambios en v1.2 (14 oct 2026):** apagar la alarma deja la alerta Atendida, como pide HU-22, en lugar de cerrarla (sección 3.1).

**Cambios en v1.1 (14 oct 2026):** las claves de dispositivo de los datos semilla van en los secretos de usuario y no en el repositorio (sección 3.3); se agrega el riesgo de que la base de datos (PostgreSQL en Docker, en la laptop servidor) no esté arriba en la demo (sección 8).

## 1. Objetivo

El MVP es la versión más pequeña de TransitGate que ya cumple los tres requisitos del profesor y demuestra la idea completa del proyecto:

- La lógica está en el servidor (la laptop).
- El sistema se monitorea desde el celular, por la web.
- Funcionan los dos controladores (ESP32 y ESP32-CAM) y los dos actuadores (motor del brazo y sirena).

Con el plan de 7 semanas, el MVP se cierra al final de la semana 5 (6 de noviembre) y deja dos semanas para los Must restantes y la prueba final. Sirve como entrega de avance y como red de seguridad: si el semestre se complica, con el MVP hay demo. Todo lo que no está en el MVP se agrega después sin cambiar lo construido (sección 4).

## 2. Guion de la demo del MVP

Este guion es la prueba de aceptación del MVP. Se considera terminado cuando el equipo lo ejecuta completo, en la red de la demo, sin tocar código ni reiniciar nada.

1. **Registro.** Desde un celular, un cliente crea su cuenta en el portal, acepta el tratamiento de datos y queda con una tarjeta de saldo 0.
2. **Solicitud.** El cliente solicita la tarjeta física; aparece en la lista de solicitudes del panel.
3. **Entrega en taquilla.** El operador elige la solicitud, pone el torniquete en modo registro y acerca una tarjeta MIFARE nueva. El cliente confirma la recepción en el portal y la tarjeta queda `Activa`.
4. **Recarga.** El cliente recarga 10.000 con pago simulado y ve el saldo nuevo sin recargar la página.
5. **Paso normal.** El cliente acerca la tarjeta: el brazo abre en menos de 1 s, el saldo baja a 6.800 en vivo, la persona cruza y el brazo cierra.
6. **Rechazo.** Una tarjeta sin saldo se rechaza; el panel muestra el intento con el motivo.
7. **Paso expirado.** Se valida y nadie cruza: a los 10 s (más la gracia) el brazo cierra solo.
8. **Colado.** Se valida una tarjeta y pasan dos personas: suena la sirena, el brazo queda bloqueado y la alerta aparece en el panel del celular del personal.
9. **Atención.** El personal apaga la alarma desde el panel en su celular; el torniquete vuelve a operar.
10. **Sin conexión.** Se apaga el servidor y se acerca una tarjeta: el brazo no abre y no se cobra nada. Se enciende de nuevo y el siguiente paso funciona sin reiniciar el ESP32.
11. **Historial.** El cliente ve en el portal sus viajes con fecha y tarifa.

## 3. Alcance funcional

De los 33 RF Must, el MVP incluye 28. Los 5 restantes son Must de la entrega final, pero el guion funciona sin ellos.

### 3.1 Incluidos en el MVP

| Módulo | RF | Simplificación permitida en el MVP |
| --- | --- | --- |
| Clientes y autenticación | RF-01, RF-02, RF-03, RF-06 | Un solo rol de personal (`Operador`) que hace taquilla y seguridad; el administrador existe solo como dato semilla |
| Tarjetas | RF-07, RF-08, RF-09, RF-10, RF-11 | Solo tarjeta física |
| Saldo | RF-15, RF-16 | — |
| Validación de acceso | RF-18, RF-20, RF-21, RF-22, RF-23, RF-25, RF-45 | El firmware ya deja listo el SELECT AID (RF-18 CA5), pero sin app no se usa |
| Detección de colados | RF-26, RF-27, RF-28, RF-29 | Gracia de visión fija en 1 s hasta que se mida (S13) |
| Monitoreo | RF-31, RF-32, RF-33, RF-34, RF-36 | Historial de intentos sin filtros; apagar la alarma deja la alerta Atendida y no hace falta clasificarla (RF-35 va en la entrega final) |
| Historial del cliente | RF-39 | Lista simple, sin paginación |

### 3.2 Después del MVP (entrega final)

| Tipo | RF | Nota |
| --- | --- | --- |
| Must pendientes | RF-13, RF-14, RF-35, RF-41, RF-46 | RF-13 se cumple solo mientras no exista el celular; RF-41 se reemplaza en el MVP por la tarifa de 3.200 en la configuración; RF-46 es el más importante de esta lista y va primero |
| Should | RF-04, RF-05, RF-12, RF-17, RF-19, RF-24, RF-30, RF-37, RF-42, RF-43 | RF-12 y RF-19 son el celular como tarjeta: carril opcional del plan de trabajo, solo si el MVP llega a tiempo |
| Could | RF-38, RF-40, RF-44 | Solo si sobra tiempo |
| Won't | Sección 11 de los RF | No se hacen en esta versión |

### 3.3 Datos semilla del MVP

Existen desde el primer arranque del servidor: el torniquete `tq_1` con sus claves de dispositivo (ESP32 y visión), un administrador, un operador y la tarifa de 3.200. Las claves y las contraseñas iniciales no van en el repositorio: se cargan desde los secretos de usuario de la laptop servidor (RNF-25).

## 4. Por qué este corte no genera retrabajo

- El modelo de datos del MVP ya tiene todas las entidades (incluidos `SoporteTarjeta` y `PasoAutorizado`), aunque el soporte virtual no se use todavía.
- El contrato de la API es el mismo: el MVP implementa un subconjunto de rutas, no rutas distintas.
- El firmware ya maneja el latido con estado deseado y el `lecturaId`, que son la base de RF-46 y del celular.
- Los roles de Seguridad y Administrador se agregan como roles nuevos sobre el mismo mecanismo de RF-06.

## 5. Requisitos no funcionales del MVP

Se verifican en el MVP los que protegen la demo y los que el profesor podría revisar primero. El resto se mide en la Fase 5.

| RNF | Qué se exige en el MVP |
| --- | --- |
| RNF-01 | El brazo abre en ≤ 1 s con tarjeta física (p95 en 50 pasadas) |
| RNF-04 | El estado deseado se aplica en ≤ 500 ms |
| RNF-06 | Retraso de cruce medido en al menos 100 cruces; con eso se fija la gracia |
| RNF-11, RNF-12 | El ESP32 se recupera solo tras un corte de energía y tras perder el WiFi |
| RNF-17 | Contraseñas con hash (ASP.NET Core Identity lo da) |
| RNF-19 | Un cliente no puede ver la tarjeta de otro |
| RNF-25 | Ningún secreto en el repositorio (gitleaks en el CI) |
| RNF-26 | No se guarda ninguna imagen |
| RNF-28 | No se puede registrar sin aceptar el tratamiento de datos |
| RNF-31 | Portal y panel usables en un celular de 360 px |
| RNF-35 | Funciona con MIFARE Classic 1K (NTAG queda para la entrega final) |
| RNF-36 | Funciona en el hotspot de la demo, sin la red de la universidad |
| RNF-38 | El guion de la sección 2, pasos 5 a 9, se ejecuta también solo con simuladores |

## 6. Hardware necesario

| Elemento | Estado |
| --- | --- |
| ESP32-CAM (OV3660) con adaptador ESP32-CAM-MB | Comprado |
| ESP32 DevKit (principal) | Comprado |
| Lector PN532 | Comprado |
| Servo para el brazo | Por confirmar |
| Sirena y su módulo relé o transistor | Sirena disponible; módulo por confirmar |
| Tarjetas MIFARE Classic 1K (al menos 3) y NTAG213/215 (al menos 1) | 2 MIFARE Classic 1K (tarjeta S50 y llavero, venían con el PN532); faltan 1 MIFARE de repuesto y 1 NTAG213/215 (RNF-35, entrega final) |
| Fuente de 5 V ≥ 2 A para la cámara y fuente aparte para el servo | Por confirmar |
| Maqueta del torniquete con soporte cenital para la cámara | Por construir |
| Laptop de un amigo para la red de la demo (2,4 GHz) | Disponible |

El PN532 y las tarjetas son el camino crítico: sin ellos no se puede probar el esqueleto vertical de la semana 2.

## 7. Hitos hacia el MVP

| Hito | Semana | Qué se demuestra | Responsable de la demo interna |
| --- | --- | --- | --- |
| H1 — Esqueleto vertical | 2 (16 oct) | Una tarjeta semilla abre el servo a través de la API; la cámara transmite y YOLO detecta personas desde el ángulo real | Alex |
| H2 — Dominio y web | 4 (30 oct) | Pasos 1 a 6 y 11 del guion, con la tarjeta real y con `sim-esp32` | Jeisy |
| H3 — MVP | 5 (6 nov) | Guion completo (pasos 1 a 11) | Ana |

Cada hito cierra con una demo interna de 15 minutos y un video corto como evidencia.

## 8. Riesgos y plan B

| Riesgo | Señal temprana | Plan B |
| --- | --- | --- |
| YOLO no separa a dos personas pegadas desde el ángulo del montaje | Prueba de ángulo de la semana 1 | Subir la cámara, bajar la resolución para ganar FPS o afinar con imágenes propias |
| La ESP32-CAM se reinicia | Contador de reinicios en la sesión de 30 min | Fuente de 2 A dedicada y condensador en la entrada de 5 V |
| `urequests` bloquea la lectura del PN532 | El brazo tarda más de 1 s | Cliente HTTP asíncrono o timeout de 250 ms en el latido (RNF-01) |
| El PN532 no responde por I2C | Fallo en el banco de pruebas | Pasar el módulo a SPI |
| El hotspot de la demo sale en 5 GHz o cambia la IP | Ensayo en el salón | Forzar 2,4 GHz e IP estática en la laptop servidor |
| El hardware llega tarde | Semana 2 sin PN532 | Seguir con `sim-esp32`; el backend y la web no dependen del hardware |
| La base de datos (PostgreSQL en Docker) no está arriba cuando arranca la API | La API no arranca o falla la primera lectura | Abrir Docker Desktop antes que la API y configurarlo para que inicie con Windows; comprobarlo en el ensayo en el salón |
