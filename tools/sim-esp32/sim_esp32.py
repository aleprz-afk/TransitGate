"""
Simulador del ESP32 principal (contratos de integración §11, RNF-38). Corre en el PC y habla con la API
igual que la placa: usa controlador.py y api.py del firmware (firmware/esp32-main), así que sigue las mismas
reglas de C1. El brazo y la sirena se muestran en la consola.

    python tools/sim-esp32/sim_esp32.py --servidor http://localhost:5000 --torniquete tq_1

La clave del dispositivo va en la variable de entorno SIM_ESP32_CLAVE (solo hace falta en modo estricto).
Comandos: escribe "ayuda".
"""
import argparse
import hashlib
import hmac
import os
import sys
import threading
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "firmware", "esp32-main"))

from api import Api, SinRespuesta  # noqa: E402
from controlador import Controlador  # noqa: E402
from reloj import ahora_ms  # noqa: E402

VERSION = "sim-0.1.0"
AYUDA = """Comandos:
  tarjeta <uid>                    acerca una tarjeta (UID en hex mayúsculas, 8 o 14 caracteres)
  celular <soporteId> <claveHex>   acerca un celular: firma el desafío vigente como lo haría la app (C2)
  reiniciar                        reinicia la ESP32: brazo cerrado, arranque nuevo (RF-45 CA3)
  cortar-red <s>                   sin red durante s segundos (RF-25)
  no-confirmar                     activa o desactiva: el brazo "no abre" aunque el servidor diga abrir (RF-46)
  estado                           muestra el estado actual
  salir"""


class EspSimulada:
    def __init__(self, api, latido_ms=300, timeout_latido_ms=250, timeout_lectura_ms=3000):
        self._api = api
        self._latido_ms = latido_ms
        self._timeout_latido_ms = timeout_latido_ms
        self._timeout_lectura_ms = timeout_lectura_ms
        self._cerrojo = threading.Lock()
        self._sin_red_hasta = 0
        self.no_confirmar = False
        self._mostrado = None
        self._reiniciar()

    # Arranque y estado

    def _reiniciar(self):
        arranque = os.urandom(2).hex()
        self.c = Controlador(arranque, ahora_ms())
        self._mostrado = None
        print("[inicio] arranque %s: brazo cerrado, sirena apagada" % arranque)

    def reiniciar(self):
        with self._cerrojo:
            self._reiniciar()

    def cortar_red(self, segundos):
        with self._cerrojo:
            self._sin_red_hasta = ahora_ms() + int(segundos * 1000)
        print("[red] cortada %g s" % segundos)

    def _hay_red(self):
        return ahora_ms() >= self._sin_red_hasta

    def _aplicar(self):
        """Los actuadores obedecen al instante, salvo el brazo con no-confirmar (RF-46)."""
        c = self.c
        brazo = c.brazo_deseado and not self.no_confirmar
        c.aplicado(brazo, c.sirena_deseada)
        actual = (brazo, c.sirena_deseada, c.modo_registro, c.conectado, c.brazo_deseado)
        if actual != self._mostrado:
            antes = self._mostrado
            self._mostrado = actual
            if antes is None or antes[0] != brazo:
                print("[brazo] " + ("ABIERTO" if brazo else "cerrado"))
            if c.brazo_deseado and not brazo and (antes is None or not antes[4]):
                print("[brazo] el servidor pide abrir, pero no-confirmar lo deja cerrado: el latido no confirma el paso (RF-46)")
            if antes is None or antes[1] != c.sirena_deseada:
                print("[sirena] " + ("ENCENDIDA" if c.sirena_deseada else "apagada"))
            if antes is None or antes[2] != c.modo_registro:
                print("[modo registro] " + ("activo: acerque la tarjeta nueva" if c.modo_registro else "no"))
            if antes is None or antes[3] != c.conectado:
                print("[conexion] " + ("en línea" if c.conectado else "sin conexión"))

    def estado(self):
        with self._cerrojo:
            c = self.c
            return ("arranque %s · brazo %s · sirena %s · modo registro %s · %s · no-confirmar %s" % (
                c.arranque, "abierto" if c.brazo_real else "cerrado", "encendida" if c.sirena_real else "apagada",
                "sí" if c.modo_registro else "no", "en línea" if c.conectado else "sin conexión",
                "sí" if self.no_confirmar else "no"))

    # Latido cada 300 ms (S11)

    def latir_siempre(self):
        while True:
            inicio = ahora_ms()
            with self._cerrojo:
                if self._hay_red():
                    try:
                        estado, respuesta = self._api.latido(self.c.cuerpo_latido(VERSION), self._timeout_latido_ms)
                        if estado == 200:
                            self.c.latido_ok(respuesta, ahora_ms())
                        else:
                            print("[latido] HTTP %d %s" % (estado, respuesta))
                    except SinRespuesta:
                        pass
                if self.c.revisar(ahora_ms()):
                    print("[conexion] 10 s sin latidos: brazo cerrado y sirena apagada (RF-25 CA2)")
                self._aplicar()
            time.sleep(max(0, self._latido_ms - (ahora_ms() - inicio)) / 1000)

    # Lecturas

    def tarjeta(self, uid):
        uid = uid.upper()
        with self._cerrojo:
            lectura_id = self.c.lectura_nueva(uid, ahora_ms())
            if lectura_id is None:
                print("[lectura] ignorada: UID inválido o la misma tarjeta hace menos de 3 s (RF-18 CA3)")
                return
            respuesta = self._enviar(lambda: self._api.lectura(lectura_id, uid, self._timeout_lectura_ms))
            print("[lectura] %s %s: %s" % (lectura_id, uid, self.c.resultado_lectura(respuesta)))
            self._aplicar()

    def celular(self, soporte_id, clave_hex):
        with self._cerrojo:
            desafio = self.c.desafio
            if not desafio:
                print("[celular] todavía no hay desafío: espera un latido")
                return
            # C2 §3.3: mensaje = "TG1" || torniqueteId || 0x00 || desafio (16 bytes)
            mensaje = b"TG1" + self._api.torniquete_id.encode() + b"\x00" + bytes.fromhex(desafio["valor"])
            firma = hmac.new(bytes.fromhex(clave_hex), mensaje, hashlib.sha256).hexdigest().upper()
            lectura_id = self.c.nuevo_lectura_id()
            respuesta = self._enviar(lambda: self._api.lectura_celular(
                lectura_id, soporte_id, desafio["desafioId"], firma, self._timeout_lectura_ms))
            print("[celular] %s: %s" % (lectura_id, self.c.resultado_lectura(respuesta)))
            self._aplicar()

    def _enviar(self, peticion):
        """C1: un intento y un reintento con el mismo lecturaId; sin respuesta no se abre (RF-25 CA1)."""
        for intento in (1, 2):
            if not self._hay_red():
                print("[lectura] intento %d: sin red" % intento)
                continue
            try:
                estado, respuesta = peticion()
                if estado == 200:
                    return respuesta
                print("[lectura] HTTP %d %s" % (estado, respuesta))
                return None
            except SinRespuesta as error:
                print("[lectura] intento %d sin respuesta (%s)" % (intento, error))
        return None


def main():
    # En Windows, la consola o una tubería pueden venir en cp1252: los acentos del log necesitan UTF-8.
    for flujo in (sys.stdout, sys.stdin):
        if hasattr(flujo, "reconfigure"):
            flujo.reconfigure(encoding="utf-8")

    argumentos = argparse.ArgumentParser(description="Simulador del ESP32 principal (RNF-38)")
    argumentos.add_argument("--servidor", default="http://localhost:5000")
    argumentos.add_argument("--torniquete", default="tq_1")
    opciones = argumentos.parse_args()

    api = Api(opciones.servidor, opciones.torniquete, os.environ.get("SIM_ESP32_CLAVE", "sin-clave"))
    esp = EspSimulada(api)
    threading.Thread(target=esp.latir_siempre, daemon=True).start()
    print("sim-esp32 contra %s, %s. Escribe \"ayuda\"." % (opciones.servidor, opciones.torniquete))

    for linea in sys.stdin:
        partes = linea.split()
        if not partes:
            continue
        comando, args = partes[0].lower(), partes[1:]
        try:
            if comando == "tarjeta" and len(args) == 1:
                esp.tarjeta(args[0])
            elif comando == "celular" and len(args) == 2:
                esp.celular(args[0], args[1])
            elif comando == "reiniciar":
                esp.reiniciar()
            elif comando == "cortar-red" and len(args) == 1:
                esp.cortar_red(float(args[0]))
            elif comando == "no-confirmar":
                esp.no_confirmar = not esp.no_confirmar
                print("[no-confirmar] " + ("activo: el brazo no abrirá" if esp.no_confirmar else "apagado"))
            elif comando == "estado":
                print(esp.estado())
            elif comando in ("salir", "exit", "quit"):
                break
            else:
                print(AYUDA)
        except ValueError as error:
            print("[error] " + str(error))


if __name__ == "__main__":
    main()
