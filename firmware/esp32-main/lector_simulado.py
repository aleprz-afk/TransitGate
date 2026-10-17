# Lector de prueba mientras no está el PN532. Tiene la misma interfaz que tendrá el real (leer() → UID o None):
#  - el botón BOOT (GPIO 0) "acerca" la siguiente tarjeta de la lista uidsPrueba de config.json;
#  - por el puerto serie se puede escribir un UID y Enter (por ejemplo 04A1B2C3) para "acercar" esa tarjeta.
import select
import sys

from machine import Pin


class LectorSimulado:
    def __init__(self, uids, pin_boton=0):
        self._uids = uids or ["04A1B2C3"]
        self._siguiente = 0
        self._boton = Pin(pin_boton, Pin.IN, Pin.PULL_UP)
        self._antes = 1
        self._entrada = select.poll()
        self._entrada.register(sys.stdin, select.POLLIN)
        self._linea = ""

    def leer(self):
        # Botón: cuenta al presionarlo (paso de 1 a 0), no mientras sigue presionado.
        ahora = self._boton.value()
        presionado = self._antes == 1 and ahora == 0
        self._antes = ahora
        if presionado:
            uid = self._uids[self._siguiente % len(self._uids)]
            self._siguiente += 1
            return uid

        # Puerto serie: lo que se escribió hasta el Enter.
        while self._entrada.poll(0):
            caracter = sys.stdin.read(1)
            if caracter in ("\r", "\n"):
                uid, self._linea = self._linea.strip().upper(), ""
                if uid:
                    return uid
            else:
                self._linea += caracter
        return None
