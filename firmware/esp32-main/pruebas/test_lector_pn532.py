# Pruebas del lector PN532 en el PC (CPython), con un PN532 de mentira que habla el mismo protocolo por I2C:
#   python -m unittest discover -s firmware/esp32-main/pruebas
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import lector_pn532  # noqa: E402
from lector_pn532 import ACK, LectorPn532, datos_respuesta, trama, uid_de_busqueda  # noqa: E402


class Reloj:
    def __init__(self):
        self.ms = 1000
        self.dormido_ms = 0     # cuánto se durmió: dentro de leer() tiene que ser 0

    def __call__(self):
        return self.ms

    def dormir(self, ms):
        self.ms += ms
        self.dormido_ms += ms


def respuesta(comando, datos=b""):
    cuerpo = bytes([0xD5, comando + 1]) + bytes(datos)
    largo = len(cuerpo)
    return (b"\x00\x00\xff" + bytes([largo, (0x100 - largo) & 0xFF]) + cuerpo
            + bytes([(0x100 - sum(cuerpo)) & 0xFF, 0x00]))


def busqueda_con(uid):
    return bytes([0x01, 0x01, 0x00, 0x04, 0x08, len(uid)]) + uid


class Pn532Falso:
    """Contesta cada comando con ACK y luego con su respuesta, poniendo el bit de listo como el real."""

    def __init__(self):
        self.tarjetas = []          # lo que verá cada búsqueda: bytes del UID o None
        self.conectado = True
        self.ocupado_sin_contestar = False   # como el real: mientras procesa, no contesta (ENODEV)
        self.sin_ack = False                 # entrega la respuesta sin mostrar antes el ACK
        self.sin_contestar = 0      # escrituras que no contesta al despertar (ENODEV)
        self.demora_lecturas = 0    # cuántas veces dice "no listo" antes de responder la búsqueda
        self.escrituras = []
        self._pendientes = []
        self._demora = 0

    def writeto(self, direccion, datos):
        if not self.conectado or direccion != 0x24:
            raise OSError(19)   # ENODEV
        if self.sin_contestar:
            self.sin_contestar -= 1
            raise OSError(19)
        self.escrituras.append(bytes(datos))
        if len(datos) < 7:
            return
        comando = datos[6]
        if comando == 0x02:
            cuerpo = b"\x32\x01\x06\x07"
        elif comando == 0x4A:
            uid = self.tarjetas.pop(0) if self.tarjetas else None
            cuerpo = busqueda_con(uid) if uid else b"\x00"
            self._demora = self.demora_lecturas
        else:
            cuerpo = b""
        self._pendientes = ([] if self.sin_ack else [ACK]) + [respuesta(comando, cuerpo)]

    def readfrom(self, direccion, largo):
        if not self.conectado:
            raise OSError(19)
        if not self._pendientes:
            return b"\x00" * largo
        if len(self._pendientes) == 1 and self._demora:
            if largo == 1:
                self._demora -= 1
            if self.ocupado_sin_contestar:
                raise OSError(19)
            return b"\x00" * largo
        if largo == 1:
            return b"\x01"
        actual = self._pendientes.pop(0)
        return (b"\x01" + actual + b"\x00" * largo)[:largo]

    def busquedas(self):
        return sum(1 for e in self.escrituras if len(e) > 6 and e[6] == 0x4A)


def nuevo(pn532=None):
    pn532 = pn532 or Pn532Falso()
    reloj = Reloj()
    lector = LectorPn532(21, 22, i2c=pn532, reloj=reloj, dormir_ms=reloj.dormir)
    return lector, pn532, reloj


def leer_hasta_uid(lector, reloj, veces=10):
    for _ in range(veces):
        uid = lector.leer()
        reloj.ms += 20      # el bucle de main.py
        if uid:
            return uid
    return None


class TramasTests(unittest.TestCase):
    def test_trama_de_get_firmware_version_es_la_del_manual(self):
        self.assertEqual(bytes.fromhex("0000FF02FED4022A00"), trama(0x02))

    def test_respuesta_con_suma_errada_no_se_acepta(self):
        buena = bytearray(respuesta(0x4A, busqueda_con(b"\x04\xa1\xb2\xc3")))
        self.assertIsNotNone(datos_respuesta(bytes(buena), 0x4A))
        buena[-2] ^= 0xFF
        self.assertIsNone(datos_respuesta(bytes(buena), 0x4A))

    def test_respuesta_de_otro_comando_no_se_acepta(self):
        self.assertIsNone(datos_respuesta(respuesta(0x02, b"\x32\x01\x06\x07"), 0x4A))

    def test_uid_de_4_bytes_da_8_caracteres_en_mayusculas(self):  # RNF-35
        self.assertEqual("04A1B2C3", uid_de_busqueda(busqueda_con(b"\x04\xa1\xb2\xc3")))

    def test_uid_de_7_bytes_da_14_caracteres(self):  # NTAG
        self.assertEqual("04112233445566", uid_de_busqueda(busqueda_con(bytes.fromhex("04112233445566"))))

    def test_sin_tarjeta_no_hay_uid(self):
        self.assertIsNone(uid_de_busqueda(b"\x00"))
        self.assertIsNone(uid_de_busqueda(None))


class LectorTests(unittest.TestCase):
    def test_al_iniciar_configura_el_pn532(self):
        lector, pn532, _ = nuevo()
        self.assertEqual("1.6", lector.version)
        comandos = [e[6] for e in pn532.escrituras if len(e) > 6]
        self.assertEqual([0x02, 0x14, 0x32], comandos)

    def test_devuelve_el_uid_de_la_tarjeta_acercada(self):
        lector, pn532, reloj = nuevo()
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))

    def test_cada_llamada_hace_un_solo_paso_y_no_espera(self):  # RNF-01: no bloquea el latido
        lector, pn532, reloj = nuevo()
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        pn532.demora_lecturas = 5
        inicio = reloj.ms
        resultados = [lector.leer() for _ in range(10)]
        self.assertEqual(inicio, reloj.ms)      # leer() nunca durmió
        self.assertIn("04A1B2C3", resultados)

    def test_sin_tarjeta_devuelve_none_y_vuelve_a_buscar(self):
        lector, pn532, reloj = nuevo()
        self.assertIsNone(leer_hasta_uid(lector, reloj, veces=9))
        self.assertGreaterEqual(pn532.busquedas(), 3)

    def test_la_tarjeta_quieta_se_reporta_en_cada_busqueda(self):  # controlador.py descarta las repetidas
        lector, pn532, reloj = nuevo()
        pn532.tarjetas = [b"\xde\xad\xbe\xef"] * 2
        self.assertEqual("DEADBEEF", leer_hasta_uid(lector, reloj))
        self.assertEqual("DEADBEEF", leer_hasta_uid(lector, reloj))

    def test_si_tarda_en_despertar_reintenta_al_iniciar(self):
        pn532 = Pn532Falso()
        pn532.sin_contestar = 3     # el byte para despertarlo y dos intentos más
        lector, _, reloj = nuevo(pn532)
        self.assertTrue(lector._listo)
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))

    def test_mientras_esta_ocupado_y_no_contesta_sigue_esperando(self):  # lo que pasó con el módulo real
        lector, pn532, reloj = nuevo()
        pn532.ocupado_sin_contestar = True
        pn532.demora_lecturas = 5
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))
        self.assertTrue(lector._listo)

    def test_si_no_muestra_el_ack_acepta_la_respuesta_directa(self):  # también visto en el módulo real
        pn532 = Pn532Falso()
        pn532.sin_ack = True
        lector, _, reloj = nuevo(pn532)
        self.assertTrue(lector._listo)
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))

    def test_sin_pn532_al_arrancar_no_se_cae(self):
        pn532 = Pn532Falso()
        pn532.conectado = False
        lector, _, reloj = nuevo(pn532)
        self.assertIsNone(leer_hasta_uid(lector, reloj))

    def test_si_se_desconecta_se_recupera_solo(self):
        lector, pn532, reloj = nuevo()
        pn532.conectado = False
        self.assertIsNone(lector.leer())
        pn532.conectado = True
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        self.assertIsNone(lector.leer())              # todavía no pasan los 2 s
        reloj.ms += lector_pn532.REINICIO_MS
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))

    def test_el_reinicio_no_duerme_dentro_de_leer(self):  # RNF-01: lo que bloqueaba el latido en la placa
        lector, pn532, reloj = nuevo()
        pn532.conectado = False
        lector.leer()                                  # se pierde la conexión
        pn532.conectado = True
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        reloj.ms += lector_pn532.REINICIO_MS
        reloj.dormido_ms = 0
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))
        self.assertEqual(0, reloj.dormido_ms)

    def test_el_reinicio_espera_al_pn532_entre_llamadas(self):
        lector, pn532, reloj = nuevo()
        pn532.conectado = False
        lector.leer()
        pn532.conectado = True
        reloj.ms += lector_pn532.REINICIO_MS
        antes = reloj.ms
        self.assertIsNone(lector.leer())               # lo despierta y vuelve sin esperar los 50 ms
        self.assertEqual(antes, reloj.ms)
        self.assertFalse(lector._listo)
        reloj.ms += lector_pn532.DESPERTAR_MS
        lector.leer()                                  # pasado el despertar, termina el inicio
        self.assertTrue(lector._listo)
        self.assertEqual("1.6", lector.version)

    def test_si_el_reinicio_falla_tampoco_bloquea_y_vuelve_a_intentar(self):
        lector, pn532, reloj = nuevo()
        pn532.conectado = False
        lector.leer()
        reloj.dormido_ms = 0
        for _ in range(3):                             # tres reinicios fallidos seguidos
            reloj.ms += lector_pn532.REINICIO_MS
            for _ in range(5):
                self.assertIsNone(lector.leer())
                reloj.ms += 20
        self.assertEqual(0, reloj.dormido_ms)
        self.assertFalse(lector._listo)
        pn532.conectado = True
        pn532.tarjetas = [b"\x04\xa1\xb2\xc3"]
        reloj.ms += lector_pn532.REINICIO_MS
        self.assertEqual("04A1B2C3", leer_hasta_uid(lector, reloj))

    def test_si_no_contesta_al_reiniciar_se_rinde_por_plazo(self):
        lector, pn532, reloj = nuevo()
        pn532.conectado = False
        lector.leer()
        pn532.conectado = True
        reloj.ms += lector_pn532.REINICIO_MS
        lector.leer()
        reloj.ms += lector_pn532.DESPERTAR_MS
        pn532.readfrom = lambda direccion, largo: b"\x00" * largo   # nunca dice "listo"
        lector.leer()                                  # escribe GetFirmwareVersion y espera el ACK
        reloj.ms += lector_pn532.PLAZO_COMANDO_MS + 1
        lector.leer()
        self.assertFalse(lector._listo)
        self.assertIsNone(lector._inicio)              # abandonó este intento; el próximo, en 2 s

    def test_si_deja_de_responder_lo_reinicia(self):
        lector, pn532, reloj = nuevo()
        lector.leer()                                  # manda la búsqueda
        pn532._pendientes = []                         # y el PN532 se queda mudo
        reloj.ms += lector_pn532.PLAZO_RESPUESTA_MS + 1
        lector.leer()
        self.assertFalse(lector._listo)


if __name__ == "__main__":
    unittest.main()
