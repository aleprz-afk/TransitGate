# vision

Servicio de visión en Python (YOLO + ByteTrack + línea virtual). Dueño: Wili.

Lee el MJPEG del ESP32-CAM (C3), detecta personas con YOLO preentrenado (clase `person`), las sigue con ByteTrack y reporta a la API cada cruce de la línea virtual y un latido cada 5 s (C4, Contrato API §7). Los fotogramas solo viven en memoria: no se guarda ninguna imagen (RNF-26).

## Estado (25 de octubre)

| Parte | Estado |
| --- | --- |
| Detección y tracking | Probado sobre el stream real: ~25 FPS procesados con YOLO nano a 320 px en el CPU (Ryzen 5 9600X; RNF-10 pide 10), 8 ms por cuadro. El límite lo pone la cámara, no el modelo |
| Conteo con franja (`conteo.py`) | Hecho, con pruebas. Falta medirlo con personas en vista cenital (RNF-07 a RNF-09) |
| Cliente de la API (`api.py`) | Probado contra la API en modo estricto: `consumido` (con `sim-esp32`), `fraude`, `registrado`, `duplicado`, 401 con clave mala y latido |
| Entrenamiento propio | No hace falta por ahora: se usa el preentrenado (Arquitectura, "YOLO preentrenado primero"). Solo si la prueba cenital no llega a las metas (RNF §10.3) |

## Archivos

- `servicio.py`: el servicio. YOLO + ByteTrack sobre el último cuadro, conteo y reporte. Con `--ver`, si la cámara deja de mandar cuadros la ventana muestra "SIN SENAL" en vez de congelarse.
- `stream.py`: lee el MJPEG con un socket propio, en un hilo, y se queda solo con el último cuadro. No usa `cv2.VideoCapture`: su lector HTTP se atascaba con la ESP32 (huecos de más de 1 s y caídas a 2 FPS que la cámara no tenía) y al cortarse podía bloquearse ~30 s. Sin datos en 3 s, reconecta.
- `conteo.py`: el conteo de cruces sin cámara ni red. Cada persona tiene un lado confirmado de la línea; alrededor de la línea hay una franja (`margen_linea`) donde no cambia nada, así que el temblor del recuadro encima de la línea no cuenta dobles. Cada `trackId` se reporta una sola vez (RF-26 CA3).
- `api.py`: cruces y latido. Los cruces salen por una cola en otro hilo, con plazo de 1 s y hasta 3 reintentos con el mismo cuerpo (C4). Anota el retraso de cada cruce en `cruces.csv` para RNF-06.
- `config.yaml`: configuración de C3 §4.1, con los valores de la red de la demo.
- `pruebas/`: pruebas del conteo y del cliente en el PC.

## Instalar

Una vez, desde la raíz del repo (Python 3.12 o más nuevo; probado con 3.14):

```
python -m venv vision\.venv
vision\.venv\Scripts\python.exe -m pip install -r vision\requirements.txt
```

Instala PyTorch para CPU. Los pesos `yolov8n.pt` se descargan solos la primera vez. `.venv`, los pesos y `cruces.csv` no se suben al repositorio.

## Usar

En la red de la demo, con la clave de la visión en la variable de entorno (RNF-25):

```
set TG_VISION_KEY=<clave de la visión>
vision\.venv\Scripts\python.exe vision\servicio.py
```

En otra red se cambian la API y la cámara por la línea de comandos:

```
vision\.venv\Scripts\python.exe vision\servicio.py --api http://127.0.0.1:5000 --stream http://192.168.1.9:81/stream --ver
```

| Opción | Qué hace |
| --- | --- |
| `--ver` | Abre una ventana con los recuadros, los `trackId`, la línea y la franja. Sirve para calibrar el montaje. Teclas: `q` sale, `r` reinicia los contadores |
| `--sin-api` | Solo detecta y cuenta; no reporta nada |
| `--csv ''` | No escribe `cruces.csv` |
| `--config <ruta>` | Otro archivo de configuración |

Para la API en la misma laptop usa `127.0.0.1` y no `localhost`: en Windows `localhost` prueba primero IPv6 y tarda ~2 s en cada petición, más que el plazo de 1 s. El cliente hace el cambio por su cuenta, pero conviene escribirlo así.

En la consola, cada línea empieza con su tema: `[camara]`, `[conteo]`, `[cruce]`, `[latido]`.

## Pruebas

```
python -m unittest discover -s vision/pruebas
```

No necesitan YOLO ni cámara: corren con el Python normal.
