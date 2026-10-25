# esp32-cam

Firmware del ESP32-CAM en Arduino (OV3660), basado en el ejemplo CameraWebServer de Espressif. Dueño: Wili.

Solo transmite video: no analiza nada (Arquitectura §3). El servicio de visión lee el stream MJPEG (contrato de integración C3).

## Estado (25 de octubre)

| Parte | Estado |
| --- | --- |
| Cámara | Probada en la placa: OV3660, PSRAM de 4 MB, VGA 640×480 con calidad JPEG 12 |
| Stream MJPEG (`:81/stream`) | Probado desde el PC por WiFi: 24,6 FPS con ~12 KB por cuadro (RNF-37 pide ≥ 15) |
| WiFi | Probado con DHCP en la red de casa; la IP fija de la demo (`192.168.137.21`) falta probarla en esa red |
| Montaje | Falta: la orientación se ajusta con `VOLTEAR_VERTICAL` y `ESPEJO_HORIZONTAL` en `config.h` |

## Rutas

| Ruta | Para qué |
| --- | --- |
| `http://<ip>/` | Página con el video, para mirar el encuadre al montar la cámara |
| `http://<ip>/capture` | Un JPEG suelto |
| `http://<ip>:81/stream` | MJPEG (`multipart/x-mixed-replace`) para el servicio de visión |

El stream va en otro puerto para que la página y `/capture` respondan aunque la visión esté conectada.

## Archivos

- `camara/camara.ino`: cámara, WiFi y los dos servidores HTTP. Mientras hay un cliente en el stream, imprime cada 5 s los FPS, el tamaño del último cuadro y el RSSI.
- `camara/config.example.h`: copia como `config.h` y llénalo (red, IP fija y orientación). `config.h` no se sube al repositorio (RNF-25).

## Programar la placa

La placa va sobre la base ESP32-CAM-MB (micro-USB con CH340), que hace el reinicio automático: no hay que presionar botones para grabar.

Herramientas, una sola vez. Se usa el `arduino-cli` que trae el Arduino IDE 2 (o el IDE mismo, con la placa "AI Thinker ESP32-CAM"):

```
set CLI=%LOCALAPPDATA%\Programs\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe
"%CLI%" core install esp32:esp32 --additional-urls https://espressif.github.io/arduino-esp32/package_esp32_index.json
```

Compilar y grabar (desde la raíz del repo; cambia el puerto por el de tu placa):

```
"%CLI%" compile --fqbn esp32:esp32:esp32cam --upload -p COM6 firmware\esp32-cam\camara
```

Probado con el core `esp32:esp32` 3.3.12. Al arrancar imprime por el puerto serie (115200) la IP y las rutas.

## Alimentación

Para probar en la mesa basta el USB de la base. En el montaje final va con fuente de 5 V ≥ 2 A (C3, RNF-37): con el WiFi transmitiendo, el USB de algunos PC no da suficiente corriente y la placa se reinicia ("Brownout detector").
