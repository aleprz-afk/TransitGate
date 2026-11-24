# Actuadores y LED de estado (C8). Solo hacen lo que main.py les pide; las decisiones son del servidor.
from machine import PWM, Pin

from reloj import ahora_ms


class Brazo:
    """Servo en PWM a 50 Hz. Pulso de 0,5 ms = cerrado (0°); 1,5 ms = abierto (90°)."""

    CERRADO_NS = 500_000
    ABIERTO_NS = 1_500_000

    def __init__(self, pin):
        # En MicroPython 1.29 el ancho de pulso va después de crear el PWM: junto con freq falla.
        self._pwm = PWM(Pin(pin), freq=50)
        self._pwm.duty_ns(self.CERRADO_NS)
        self.abierto = False

    def poner(self, abierto):
        if abierto != self.abierto:
            self._pwm.duty_ns(self.ABIERTO_NS if abierto else self.CERRADO_NS)
            self.abierto = abierto
            print("[brazo] " + ("ABIERTO" if abierto else "cerrado"))
        return self.abierto


class Sirena:
    """Salida digital hacia el transistor o el relé: el ESP32 trabaja a 3,3 V, nunca la sirena directo (C8)."""

    def __init__(self, pin):
        self._pin = Pin(pin, Pin.OUT, value=0)
        self.encendida = False

    def poner(self, encendida):
        if encendida != self.encendida:
            self._pin.value(1 if encendida else 0)
            self.encendida = encendida
            print("[sirena] " + ("ENCENDIDA" if encendida else "apagada"))
        return self.encendida


class SirenaTono:
    """Zumbador pasivo directo al pin (sin relé): solo suena con una señal que oscila, no con corriente fija.

    Alterna dos tonos para que suene como sirena. Se usa con "sirena": "tono" en config.json; el pin da
    3,3 V y pocos mA, suficiente para un piezo pasivo (más bajo que a 12 V, pero se oye).
    """

    TONOS_HZ = (2000, 3000)
    CAMBIO_MS = 400
    VOLUMEN = 32768   # duty_u16 al 50 %: el máximo volumen de un piezo

    def __init__(self, pin):
        # Igual que el servo: el duty va después de crear el PWM.
        self._pwm = PWM(Pin(pin), freq=self.TONOS_HZ[0])
        self._pwm.duty_u16(0)
        self._tono = 0
        self.encendida = False

    def poner(self, encendida):
        if encendida != self.encendida:
            self._pwm.duty_u16(self.VOLUMEN if encendida else 0)
            self.encendida = encendida
            print("[sirena] " + ("ENCENDIDA (tono)" if encendida else "apagada"))
        if encendida:
            tono = (ahora_ms() // self.CAMBIO_MS) % len(self.TONOS_HZ)
            if tono != self._tono:
                self._tono = tono
                self._pwm.freq(self.TONOS_HZ[tono])
        return self.encendida


class Led:
    """LED de la placa (C8): parpadeo = sin conexión; fijo = modo registro; apagado = todo normal."""

    def __init__(self, pin):
        self._pin = Pin(pin, Pin.OUT, value=0)

    def mostrar(self, patron):
        if patron == "parpadeo":
            self._pin.value((ahora_ms() // 500) % 2)
        else:
            self._pin.value(1 if patron == "fijo" else 0)
