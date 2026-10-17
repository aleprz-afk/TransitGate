# Milisegundos monotónicos, iguales en la placa (MicroPython) y en el PC (CPython, para las pruebas).
# En MicroPython ticks_ms() da la vuelta, así que las diferencias siempre se calculan con diferencia().
import time

try:
    _ticks_ms = time.ticks_ms
    _ticks_diff = time.ticks_diff
except AttributeError:  # CPython
    def _ticks_ms():
        return int(time.monotonic() * 1000)

    def _ticks_diff(a, b):
        return a - b


def ahora_ms():
    return _ticks_ms()


def diferencia(despues, antes):
    """Milisegundos entre dos instantes de ahora_ms()."""
    return _ticks_diff(despues, antes)
