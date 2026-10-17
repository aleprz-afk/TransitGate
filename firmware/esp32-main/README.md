# esp32-main

Firmware del ESP32 principal en MicroPython (PN532, servo, sirena). Dueña: Karol.

Lee la tarjeta, la reporta a la API, manda un latido cada 300 ms y aplica lo que el servidor diga: el servidor decide y la ESP32 obedece (contratos de integración C1 y C8).

## Estado (17 de octubre)

| Parte | Estado |
| --- | --- |
| Reglas de C1 (`controlador.py`) | Hechas, con 12 pruebas en el PC |
| Cliente HTTP con timeout (`api.py`) | Probado desde el PC contra la API real; en la placa falta probarlo con WiFi |
| WiFi (`red.py`) | Escrito; falta probarlo con la red de 2,4 GHz |
| Servo, sirena y LED (`actuadores.py`) | El PWM del servo y la sirena arrancan bien en la placa; faltan conectarlos y probarlos |
| Lector | Simulado (`lector_simulado.py`); `lector_pn532.py` llega con el PN532 |

## Archivos

- `main.py`: el bucle principal. Arranca con el brazo cerrado y la sirena apagada (RF-45 CA3); cada 20 ms revisa el lector, cada 300 ms manda el latido y aplica el estado deseado.
- `controlador.py`: las reglas de C1 sin hardware ni red: `lecturaId`, tarjeta quieta (RF-18 CA3), abrir y confirmar el paso (RF-22 CA4), cierre a los 10 s sin conexión (RF-25 CA2) y el patrón del LED.
- `api.py`: cliente HTTP con sockets y un plazo por petición: 250 ms el latido y 3 s la lectura. `urequests` no tiene timeout y bloquearía el lector (nota de RNF-01).
- `red.py`: conecta y reconecta el WiFi en segundo plano.
- `actuadores.py`: servo (PWM 50 Hz en GPIO 13), sirena (GPIO 26, por transistor o relé) y LED de la placa (GPIO 2: parpadea sin conexión, fijo en modo registro).
- `lector_simulado.py`: mientras no está el PN532. El botón BOOT "acerca" la siguiente tarjeta de `uidsPrueba`, o se escribe un UID y Enter por el puerto serie.
- `config.example.json`: copia como `config.json` y llénalo (red, IP de la laptop, clave del dispositivo). `config.json` no se sube al repositorio (RNF-25).
- `pruebas/`: pruebas del controlador en el PC.

## Programar la placa

Herramientas, una sola vez:

```
python -m pip install --user esptool mpremote
```

**Cargar MicroPython** (solo si la placa no lo tiene). Descarga `ESP32_GENERIC` de https://micropython.org/download/ESP32_GENERIC/ y, con el botón BOOT presionado mientras conecta:

```
python -m esptool --port COM5 --baud 460800 write-flash --erase-all -z 0x1000 ESP32_GENERIC-<versión>.bin
```

**Subir el firmware** (desde esta carpeta; `mpremote` reinicia la placa al abrir el puerto, por eso el `sleep 3`):

```
python -m mpremote connect COM5 sleep 3 cp actuadores.py api.py config.example.json controlador.py lector_simulado.py main.py red.py reloj.py :
python -m mpremote connect COM5 sleep 3 cp config.json :
```

**Ver lo que imprime y "acercar" tarjetas:** `python -m mpremote connect COM5`, luego Ctrl-D para reiniciar. Escribe un UID y Enter (por ejemplo `04A1B2C3`) o presiona BOOT. Ctrl-] para salir.

Cada línea del log empieza con su tema: `[red]`, `[lectura]`, `[brazo]`, `[sirena]`, `[conexion]`.

## Probar sin placa

```
python -m unittest discover -s firmware/esp32-main/pruebas
```

`api.py` y `controlador.py` también corren en el PC con CPython. `tools/sim-esp32` los usa para simular la placa completa contra la API.
