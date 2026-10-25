"""Cliente de la API para el servicio de visión (Contrato API §7, C4).

Los cruces salen por una cola en su propio hilo para que la detección nunca espere a
la red. Cada cruce se reintenta hasta 3 veces con el mismo cuerpo si la API no
responde en 1 s; la API descarta los repetidos por sesionVision + trackId.
"""
import csv
import json
import os
import queue
import threading
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

PLAZO_SEG = 1.0
REINTENTOS = 3


def hora_iso(t):
    """Instante (time.time()) en UTC, ISO-8601 con milisegundos y terminado en Z."""
    return datetime.fromtimestamp(t, timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


def nueva_sesion(t=None):
    """sesionVision: cambia en cada arranque del servicio (Contrato API §7)."""
    return "v-" + datetime.fromtimestamp(t or time.time()).strftime("%Y%m%d-%H%M%S")


class SinRespuesta(Exception):
    pass


def enviar_http(url, cuerpo, clave, plazo=PLAZO_SEG):
    """POST JSON. Devuelve (código, cuerpo como dict o None). SinRespuesta si no contesta a tiempo."""
    datos = json.dumps(cuerpo).encode()
    req = urllib.request.Request(url, data=datos, method="POST",
                                 headers={"Content-Type": "application/json"})
    if clave:
        req.add_header("X-Device-Key", clave)
    try:
        with urllib.request.urlopen(req, timeout=plazo) as r:
            texto = r.read()
            return r.status, json.loads(texto) if texto else None
    except urllib.error.HTTPError as e:
        texto = e.read()
        try:
            return e.code, json.loads(texto) if texto else None
        except ValueError:
            return e.code, None
    except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as e:
        raise SinRespuesta(str(e)) from e


class ClienteVision:
    def __init__(self, api_url, torniquete_id, sesion, clave=None, enviar=enviar_http,
                 csv_retrasos=None, log=print):
        # En Windows "localhost" prueba primero ::1, donde la API no escucha, y tarda ~2 s
        # en caer a IPv4: más que el plazo de 1 s. Se usa la dirección IPv4 directa.
        api_url = api_url.replace("://localhost", "://127.0.0.1")
        base = api_url.rstrip("/") + f"/api/torniquetes/{torniquete_id}"
        self.url_cruces = base + "/cruces"
        self.url_latido = base + "/vision/latido"
        self.sesion = sesion
        self.clave = clave
        self.enviar = enviar
        self.csv_retrasos = csv_retrasos
        self.log = log
        self._cola = queue.Queue()
        self._activo = False

    # --- cruces ---------------------------------------------------------------

    def cuerpo_cruce(self, track_id, sentido, hora_fotograma):
        return {"trackId": int(track_id), "sesionVision": self.sesion, "sentido": sentido,
                "horaFotograma": hora_iso(hora_fotograma)}

    def reportar_cruce(self, track_id, sentido, hora_fotograma):
        """Encola el cruce; lo envía el hilo de cruces."""
        self._cola.put(self.cuerpo_cruce(track_id, sentido, hora_fotograma))

    def enviar_cruce(self, cuerpo):
        """Envía un cruce con sus reintentos. Devuelve el dict de respuesta o None."""
        for intento in range(1, REINTENTOS + 2):
            try:
                codigo, resp = self.enviar(self.url_cruces, cuerpo, self.clave)
            except SinRespuesta as e:
                self.log(f"[cruce] trackId={cuerpo['trackId']} sin respuesta (intento {intento}): {e}")
                continue
            if codigo >= 500:
                self.log(f"[cruce] trackId={cuerpo['trackId']} error {codigo} (intento {intento})")
                continue
            self._anotar_retraso(cuerpo, codigo, resp, intento)
            if codigo != 200:
                self.log(f"[cruce] trackId={cuerpo['trackId']} rechazado {codigo}: {resp}")
                return None
            extra = resp.get("pasoId") or resp.get("alertaId") or ""
            self.log(f"[cruce] {cuerpo['sentido']} trackId={cuerpo['trackId']} -> {resp.get('resultado')} {extra}".rstrip())
            return resp
        self.log(f"[cruce] trackId={cuerpo['trackId']} perdido tras {REINTENTOS} reintentos")
        return None

    def _anotar_retraso(self, cuerpo, codigo, resp, intentos):
        """RNF-06: retraso desde la llegada del fotograma hasta la respuesta de la API."""
        if not self.csv_retrasos:
            return
        llegada = datetime.strptime(cuerpo["horaFotograma"], "%Y-%m-%dT%H:%M:%S.%fZ").replace(tzinfo=timezone.utc)
        retraso_ms = round((datetime.now(timezone.utc) - llegada).total_seconds() * 1000)
        nuevo = not os.path.exists(self.csv_retrasos)
        with open(self.csv_retrasos, "a", newline="") as f:
            w = csv.writer(f)
            if nuevo:
                w.writerow(["horaFotograma", "sesionVision", "trackId", "sentido", "codigo",
                            "resultado", "intentos", "retrasoMs"])
            w.writerow([cuerpo["horaFotograma"], cuerpo["sesionVision"], cuerpo["trackId"],
                        cuerpo["sentido"], codigo, (resp or {}).get("resultado", ""), intentos, retraso_ms])

    # --- latido ---------------------------------------------------------------

    def enviar_latido(self, fps):
        try:
            codigo, resp = self.enviar(self.url_latido,
                                       {"fps": round(fps, 1), "sesionVision": self.sesion}, self.clave)
        except SinRespuesta as e:
            self.log(f"[latido] sin respuesta: {e}")
            return False
        if codigo != 204:
            self.log(f"[latido] respuesta {codigo}: {resp}")
            return False
        return True

    # --- hilos ----------------------------------------------------------------

    def iniciar(self, fps_actual, latido_seg=5):
        """Arranca el hilo de cruces y el del latido (cada latido_seg, S14)."""
        self._activo = True
        threading.Thread(target=self._hilo_cruces, daemon=True).start()
        threading.Thread(target=self._hilo_latido, args=(fps_actual, latido_seg), daemon=True).start()

    def detener(self):
        self._activo = False
        self._cola.put(None)

    def _hilo_cruces(self):
        while self._activo:
            cuerpo = self._cola.get()
            if cuerpo is None:
                break
            self.enviar_cruce(cuerpo)

    def _hilo_latido(self, fps_actual, latido_seg):
        conectado = None
        while self._activo:
            ok = self.enviar_latido(fps_actual())
            if ok != conectado:
                self.log("[latido] API en línea" if ok else "[latido] API sin conexión")
                conectado = ok
            time.sleep(latido_seg)
