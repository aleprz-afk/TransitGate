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


class Led:
    """LED de la placa (C8): parpadeo = sin conexión; fijo = modo registro; apagado = todo normal."""

    def __init__(self, pin):
        self._pin = Pin(pin, Pin.OUT, value=0)

    def mostrar(self, patron):
        if patron == "parpadeo":
            self._pin.value((ahora_ms() // 500) % 2)
        else:
            self._pin.value(1 if patron == "fijo" else 0)
