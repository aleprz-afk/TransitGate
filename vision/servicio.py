"""Servicio de visión de TransitGate: YOLO + ByteTrack + línea virtual (Arquitectura §4).

Lee el MJPEG del ESP32-CAM (C3), detecta personas, las sigue y reporta a la API cada
cruce de la línea virtual y un latido cada 5 s (C4, Contrato API §7). Los fotogramas
solo viven en memoria: no se guarda ninguna imagen (RNF-26).

    python servicio.py                                  # con config.yaml (red de la demo)
    python servicio.py --api http://localhost:5000 --stream http://192.168.1.9:81/stream --ver
    python servicio.py --sin-api --ver                  # solo mirar la detección

Con --ver abre una ventana con los recuadros, la línea y la franja (calibración).
Teclas: q sale, r reinicia los contadores.
"""
import argparse
import os
import threading
import time

import cv2
import yaml
from ultralytics import YOLO

from api import ClienteVision, nueva_sesion
from conteo import ContadorCruces

PERSONA = 0  # clase person de COCO
AQUI = os.path.dirname(os.path.abspath(__file__))


class LectorStream:
    """Lee el MJPEG en un hilo y se queda solo con el último cuadro y su hora de llegada.

    cv2.VideoCapture acumula cuadros si el modelo va más lento que la cámara; así la
    detección siempre trabaja sobre lo más reciente. La hora de llegada es la
    horaFotograma del cruce (S13).
    """

    def __init__(self, url, log=print):
        self.url = url
        self.log = log
        self.recibidos = 0
        self._cuadro = None
        self._llegada = None
        self._candado = threading.Lock()
        self.activo = True
        threading.Thread(target=self._leer, daemon=True).start()

    def _leer(self):
        conectado = None
        while self.activo:
            cap = cv2.VideoCapture(self.url)
            while self.activo and cap.isOpened():
                ok, cuadro = cap.read()
                if not ok:
                    break
                llegada = time.time()
                if not conectado:
                    self.log(f"[camara] stream conectado: {self.url}")
                    conectado = True
                with self._candado:
                    self._cuadro, self._llegada = cuadro, llegada
                    self.recibidos += 1
            cap.release()
            if self.activo:
                if conectado is not False:
                    self.log("[camara] sin stream; reintentando cada segundo")
                conectado = False
                time.sleep(1)

    def ultimo(self):
        """(cuadro, hora de llegada) del cuadro más reciente no leído, o (None, None)."""
        with self._candado:
            cuadro, llegada = self._cuadro, self._llegada
            self._cuadro = None
            return cuadro, llegada


def cargar_config(ruta):
    with open(ruta, encoding="utf-8") as f:
        return yaml.safe_load(f)


def dibujar(cuadro, contador, cajas, texto):
    alto, ancho = cuadro.shape[:2]
    (x1, y1), (x2, y2) = contador.p1, contador.p2
    a, b = (int(x1 * ancho), int(y1 * alto)), (int(x2 * ancho), int(y2 * alto))
    # franja: la línea desplazada margen a cada lado, en la normal
    largo = contador._largo
    nx, ny = -(y2 - y1) / largo * contador.margen, (x2 - x1) / largo * contador.margen
    for s in (-1, 1):
        d = (int(s * nx * ancho), int(s * ny * alto))
        cv2.line(cuadro, (a[0] + d[0], a[1] + d[1]), (b[0] + d[0], b[1] + d[1]), (0, 0, 120), 1)
    cv2.line(cuadro, a, b, (0, 0, 255), 2)
    for (bx1, by1, bx2, by2), tid in cajas:
        cv2.rectangle(cuadro, (int(bx1), int(by1)), (int(bx2), int(by2)), (0, 200, 0), 2)
        cv2.putText(cuadro, f"#{tid}", (int(bx1), int(by1) - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 200, 0), 1)
        cv2.circle(cuadro, (int((bx1 + bx2) / 2), int((by1 + by2) / 2)), 3, (0, 200, 0), -1)
    cv2.rectangle(cuadro, (0, 0), (ancho, 22), (0, 0, 0), -1)
    cv2.putText(cuadro, texto, (5, 16), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (255, 255, 255), 1)


def main():
    p = argparse.ArgumentParser(description="Servicio de visión de TransitGate")
    p.add_argument("--config", default=os.path.join(AQUI, "config.yaml"))
    p.add_argument("--api", help="cambia api_url")
    p.add_argument("--stream", help="cambia stream_url")
    p.add_argument("--sin-api", action="store_true", help="no reporta nada a la API")
    p.add_argument("--ver", action="store_true", help="muestra el video con las detecciones")
    p.add_argument("--csv", default=os.path.join(AQUI, "cruces.csv"),
                   help="retrasos de los cruces para RNF-06 ('' para no escribirlo)")
    a = p.parse_args()

    c = cargar_config(a.config)
    api_url = a.api or c["api_url"]
    stream_url = a.stream or c["stream_url"]
    sesion = nueva_sesion()
    print(f"[servicio] sesionVision {sesion}, torniquete {c['torniquete_id']}")

    contador = ContadorCruces(tuple(c["linea"]["p1"]), tuple(c["linea"]["p2"]),
                              c["sentido_entrada"], c.get("margen_linea", 0.05))
    modelo = YOLO(os.path.join(AQUI, c["modelo"]) if not os.path.isabs(c["modelo"]) else c["modelo"])
    lector = LectorStream(stream_url)

    fps = {"valor": 0.0}
    cliente = None
    if not a.sin_api:
        clave = os.environ.get(c.get("device_key_env", "TG_VISION_KEY"))
        if not clave:
            print(f"[servicio] sin {c.get('device_key_env')}: los envíos van sin X-Device-Key")
        cliente = ClienteVision(api_url, c["torniquete_id"], sesion, clave, csv_retrasos=a.csv or None)
        cliente.iniciar(lambda: fps["valor"], c.get("latido_seg", 5))
        print(f"[servicio] reportando a {api_url}")

    entradas = salidas = 0
    procesados, desde, recibidos_desde, fps_cam = 0, time.time(), 0, 0.0
    try:
        while True:
            cuadro, llegada = lector.ultimo()
            if cuadro is None:
                time.sleep(0.005)
                continue

            alto, ancho = cuadro.shape[:2]
            r = modelo.track(cuadro, imgsz=c.get("imgsz", 320), conf=c["confianza_min"],
                             classes=[PERSONA], tracker="bytetrack.yaml", persist=True,
                             verbose=False, save=False)[0]
            cajas = []
            if r.boxes.id is not None:
                for caja, tid in zip(r.boxes.xyxy.tolist(), r.boxes.id.int().tolist()):
                    cajas.append((caja, tid))
                    x = (caja[0] + caja[2]) / 2 / ancho
                    y = (caja[1] + caja[3]) / 2 / alto
                    sentido = contador.actualizar(tid, x, y, llegada)
                    if sentido:
                        if sentido == "entrada":
                            entradas += 1
                        else:
                            salidas += 1
                        print(f"{time.strftime('%H:%M:%S')} [conteo] {sentido} trackId={tid}")
                        if cliente:
                            cliente.reportar_cruce(tid, sentido, llegada)
            contador.olvidar_viejos(llegada)

            procesados += 1
            ahora = time.time()
            if ahora - desde >= 2:
                fps["valor"] = procesados / (ahora - desde)
                fps_cam = (lector.recibidos - recibidos_desde) / (ahora - desde)
                procesados, desde, recibidos_desde = 0, ahora, lector.recibidos

            if a.ver:
                dibujar(cuadro, contador, cajas,
                        f"YOLO {fps['valor']:.1f} FPS | camara {fps_cam:.1f} FPS | "
                        f"personas {len(cajas)} | entradas {entradas} | salidas {salidas}")
                cv2.imshow("TransitGate - vision", cuadro)
                tecla = cv2.waitKey(1) & 0xFF
                if tecla == ord("q"):
                    break
                if tecla == ord("r"):
                    entradas = salidas = 0
                    contador.reiniciar()
    except KeyboardInterrupt:
        pass
    finally:
        lector.activo = False
        if cliente:
            cliente.detener()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
