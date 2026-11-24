# WiFi de la red de la demo (2,4 GHz, WPA2-Personal; RNF-45). Se conecta en segundo plano y se reconecta
# sola: el resto del firmware sigue funcionando sin red (sin conexión el brazo se queda cerrado, RF-25).
import network

from reloj import ahora_ms, diferencia

REINTENTO_MS = 10000


class Red:
    def __init__(self, ssid, password):
        self._ssid = ssid
        self._password = password
        self._wlan = network.WLAN(network.STA_IF)
        self._wlan.active(True)
        # Sin ahorro de energía: con él, la ESP32 solo escucha la red cada cierto tiempo y las respuestas de
        # la API se demoran cientos de ms; el latido tiene 250 ms y fallaba a ratos ("fuera de línea" intermitente).
        try:
            self._wlan.config(pm=self._wlan.PM_NONE)
        except (AttributeError, ValueError, OSError):
            pass
        self._ultimo_intento = None
        self._conectada = False
        self.configurada = bool(ssid) and ssid != "POR_DEFINIR"

    def ip(self):
        return self._wlan.ifconfig()[0]

    def revisar(self):
        """Lanza la conexión si hace falta. Devuelve True si hay red."""
        if not self.configurada:
            return False

        conectada = self._wlan.isconnected()
        if conectada != self._conectada:
            self._conectada = conectada
            print("[red] " + ("conectada: " + self._wlan.ifconfig()[0] if conectada else "sin red"))
        if conectada:
            return True

        ahora = ahora_ms()
        if self._ultimo_intento is None or diferencia(ahora, self._ultimo_intento) >= REINTENTO_MS:
            self._ultimo_intento = ahora
            print("[red] conectando a " + self._ssid)
            try:
                self._wlan.disconnect()
            except OSError:
                pass
            self._wlan.connect(self._ssid, self._password)
        return False