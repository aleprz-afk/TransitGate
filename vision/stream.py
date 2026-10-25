"""Lector del MJPEG del ESP32-CAM (C3).

No usa cv2.VideoCapture: su lector HTTP (FFmpeg) se atascaba con el stream de la ESP32,
con huecos de más de 1 s y caídas a 2 FPS que la cámara no tenía, y cuando se cortaba
podía quedarse bloqueado hasta ~30 s. Aquí se lee el HTTP con un socket con plazo, se
separan los JPEG por sus marcadores y se decodifica solo con cv2.imdecode.
"""
import socket
import threading
import time
from urllib.parse import urlsplit

INICIO_JPEG = b"\xff\xd8"
FIN_JPEG = b"\xff\xd9"
MAX_BUFFER = 2 * 1024 * 1024   # si no aparece un JPEG completo en 2 MB, algo anda mal
PLAZO_SEG = 3.0                # sin datos en este tiempo se da por cortado y se reconecta


def separar_jpegs(buf):
    """Saca del buffer los JPEG completos. Devuelve (lista de JPEG, resto del buffer)."""
    jpegs = []
    while True:
        a = buf.find(INICIO_JPEG)
        if a < 0:
            return jpegs, buf[-1:]          # puede ser la mitad de un marcador
        b = buf.find(FIN_JPEG, a + 2)
        if b < 0:
            return jpegs, buf[a:]
        jpegs.append(buf[a:b + 2])
        buf = buf[b + 2:]


class LectorStream:
    """Lee el MJPEG en un hilo y se queda solo con el último cuadro y su hora de llegada.

    La detección siempre trabaja sobre lo más reciente, sin cola. La hora de llegada es
    la horaFotograma del cruce (S13).
    """

    def __init__(self, url, log=print):
        self.url = url
        self.log = log
        self.recibidos = 0
        self.ultima_llegada = None
        self._cuadro = None
        self._llegada = None
        self._candado = threading.Lock()
        self.activo = True
        threading.Thread(target=self._leer, daemon=True).start()

    def _conectar(self):
        u = urlsplit(self.url)
        s = socket.create_connection((u.hostname, u.port or 80), timeout=PLAZO_SEG)
        ruta = (u.path or "/") + (f"?{u.query}" if u.query else "")
        s.sendall(f"GET {ruta} HTTP/1.1\r\nHost: {u.hostname}\r\nConnection: keep-alive\r\n\r\n".encode())
        s.settimeout(PLAZO_SEG)
        return s

    def _leer(self):
        import cv2
        import numpy as np

        conectado = None
        while self.activo:
            s = None
            try:
                s = self._conectar()
                buf = b""
                while self.activo:
                    datos = s.recv(65536)
                    if not datos:
                        raise ConnectionError("la cámara cerró la conexión")
                    buf += datos
                    jpegs, buf = separar_jpegs(buf)
                    if len(buf) > MAX_BUFFER:
                        buf = b""
                    if not jpegs:
                        continue
                    llegada = time.time()
                    cuadro = cv2.imdecode(np.frombuffer(jpegs[-1], np.uint8), cv2.IMREAD_COLOR)
                    if cuadro is None:
                        continue
                    if not conectado:
                        self.log(f"[camara] stream conectado: {self.url}")
                        conectado = True
                    with self._candado:
                        self._cuadro, self._llegada = cuadro, llegada
                        self.recibidos += len(jpegs)
                        self.ultima_llegada = llegada
            except OSError as e:
                if self.activo and conectado is not False:
                    self.log(f"[camara] sin stream ({e}); reintentando cada segundo")
                conectado = False
            finally:
                if s:
                    s.close()
            if self.activo:
                time.sleep(1)

    def ultimo(self):
        """(cuadro, hora de llegada) del cuadro más reciente no leído, o (None, None)."""
        with self._candado:
            cuadro, llegada = self._cuadro, self._llegada
            self._cuadro = None
            return cuadro, llegada
