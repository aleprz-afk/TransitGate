# Pruebas de las reglas de C1 en el PC (CPython), sin placa ni red:
#   python -m unittest discover -s firmware/esp32-main/pruebas
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from controlador import Controlador, uid_valido  # noqa: E402

T0 = 1000


def nuevo():
    return Controlador("a1f3", T0)


class LecturasTests(unittest.TestCase):
    def test_lectura_id_es_arranque_mas_contador_de_4_digitos(self):  # RF-18
        c = nuevo()
        self.assertEqual("a1f3-0001", c.lectura_nueva("04A1B2C3", T0))
        self.assertEqual("a1f3-0002", c.lectura_nueva("DEADBEEF", T0 + 10))

    def test_la_misma_tarjeta_quieta_no_genera_otra_lectura(self):  # RF-18 CA3
        c = nuevo()
        self.assertIsNotNone(c.lectura_nueva("04A1B2C3", T0))
        self.assertIsNone(c.lectura_nueva("04A1B2C3", T0 + 2999))
        # Sigue encima: cada detección corre el plazo.
        self.assertIsNone(c.lectura_nueva("04A1B2C3", T0 + 5000))
        self.assertEqual("a1f3-0002", c.lectura_nueva("04A1B2C3", T0 + 8001))

    def test_otra_tarjeta_si_se_lee_enseguida(self):
        c = nuevo()
        c.lectura_nueva("04A1B2C3", T0)
        self.assertIsNotNone(c.lectura_nueva("DEADBEEF", T0 + 100))

    def test_uid_en_hex_mayusculas_de_8_o_14_caracteres(self):  # RNF-35
        self.assertTrue(uid_valido("04A1B2C3"))
        self.assertTrue(uid_valido("04A1B2C3D4E5F6"))
        self.assertFalse(uid_valido("04a1b2c3"))
        self.assertFalse(uid_valido("04A1B2"))
        self.assertIsNone(nuevo().lectura_nueva("xyz", T0))

    def test_abrir_abre_ya_y_guarda_el_paso_para_confirmarlo(self):  # RF-22 CA4, S15
        c = nuevo()
        c.resultado_lectura({"accion": "abrir", "pasoId": 7, "saldoRestante": 8800})
        self.assertTrue(c.brazo_deseado)
        c.aplicado(True, False)
        self.assertEqual(7, c.cuerpo_latido("0.1.0")["pasoIdAbierto"])
        self.assertEqual("abierto", c.cuerpo_latido("0.1.0")["brazoReal"])

    def test_sin_respuesta_o_denegar_no_abre(self):  # RF-25 CA1
        c = nuevo()
        c.resultado_lectura(None)
        c.resultado_lectura({"accion": "denegar", "motivo": "SaldoInsuficiente"})
        c.resultado_lectura({"accion": "registrada", "lecturaId": "a1f3-0001"})
        self.assertFalse(c.brazo_deseado)


class LatidoTests(unittest.TestCase):
    def test_arranca_con_brazo_cerrado_y_sirena_apagada(self):  # RF-45 CA3
        cuerpo = nuevo().cuerpo_latido("0.1.0")
        self.assertEqual(("cerrado", False, None, "a1f3"),
                         (cuerpo["brazoReal"], cuerpo["sirenaReal"], cuerpo["pasoIdAbierto"], cuerpo["arranque"]))

    def test_aplica_el_estado_deseado_del_servidor(self):  # RF-45
        c = nuevo()
        c.latido_ok({"brazoDeseado": "cerrado", "sirenaDeseada": True, "pasoId": None, "modoRegistro": False}, T0)
        self.assertEqual((False, True), (c.brazo_deseado, c.sirena_deseada))
        self.assertTrue(c.conectado)

    def test_cuando_el_servidor_cierra_se_olvida_el_paso(self):
        c = nuevo()
        c.resultado_lectura({"accion": "abrir", "pasoId": 7})
        c.aplicado(True, False)
        c.latido_ok({"brazoDeseado": "cerrado", "sirenaDeseada": False}, T0)
        c.aplicado(False, False)
        self.assertIsNone(c.cuerpo_latido("0.1.0")["pasoIdAbierto"])

    def test_diez_segundos_sin_latidos_cierra_el_brazo_y_apaga_la_sirena(self):  # RF-25 CA2
        c = nuevo()
        c.latido_ok({"brazoDeseado": "abierto", "sirenaDeseada": True}, T0)
        self.assertFalse(c.revisar(T0 + 9999))
        self.assertTrue(c.brazo_deseado)

        self.assertTrue(c.revisar(T0 + 10000))
        self.assertEqual((False, False, False), (c.brazo_deseado, c.sirena_deseada, c.conectado))
        self.assertFalse(c.revisar(T0 + 10500))   # solo avisa una vez

    def test_sin_ningun_latido_el_plazo_corre_desde_el_arranque(self):
        c = nuevo()
        self.assertFalse(c.revisar(T0 + 5000))
        c.revisar(T0 + 10000)
        self.assertEqual("parpadeo", c.patron_led())

    def test_led_fijo_en_modo_registro(self):  # C8
        c = nuevo()
        c.latido_ok({"brazoDeseado": "cerrado", "sirenaDeseada": False, "modoRegistro": True}, T0)
        self.assertEqual("fijo", c.patron_led())
        c.latido_ok({"brazoDeseado": "cerrado", "sirenaDeseada": False, "modoRegistro": False}, T0)
        self.assertEqual("apagado", c.patron_led())


if __name__ == "__main__":
    unittest.main()
