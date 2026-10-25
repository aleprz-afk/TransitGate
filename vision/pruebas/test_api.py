# Pruebas del cliente de la API (C4) en el PC, con un envío falso en lugar de la red.
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from api import REINTENTOS, ClienteVision, SinRespuesta, hora_iso, nueva_sesion  # noqa: E402


class EnvioFalso:
    """Responde en orden lo que se le dé; una excepción en la lista se lanza."""

    def __init__(self, *respuestas):
        self.respuestas = list(respuestas)
        self.llamadas = []

    def __call__(self, url, cuerpo, clave):
        self.llamadas.append((url, dict(cuerpo), clave))
        r = self.respuestas.pop(0)
        if isinstance(r, Exception):
            raise r
        return r


def cliente(envio, **kw):
    return ClienteVision("http://api:5000/", "tq_1", "v-20261025-1", "clave", enviar=envio,
                         log=lambda *_: None, **kw)


class FormatoTests(unittest.TestCase):
    def test_hora_en_utc_con_milisegundos_y_z(self):  # Contrato API §7
        self.assertEqual("2026-10-25T18:30:01.450Z", hora_iso(1792953001.450))

    def test_sesion_cambia_con_la_hora_de_arranque(self):
        self.assertRegex(nueva_sesion(), r"^v-\d{8}-\d{6}$")

    def test_cuerpo_del_cruce(self):
        c = cliente(EnvioFalso())
        self.assertEqual({"trackId": 7, "sesionVision": "v-20261025-1", "sentido": "entrada",
                          "horaFotograma": "2026-10-25T18:30:01.450Z"},
                         c.cuerpo_cruce(7, "entrada", 1792953001.450))


class CrucesTests(unittest.TestCase):
    def test_envia_a_la_ruta_del_torniquete_con_la_clave(self):
        envio = EnvioFalso((200, {"resultado": "consumido", "pasoId": 5}))
        resp = cliente(envio).enviar_cruce({"trackId": 1, "sentido": "entrada", "horaFotograma": "x"})
        self.assertEqual("consumido", resp["resultado"])
        url, _, clave = envio.llamadas[0]
        self.assertEqual("http://api:5000/api/torniquetes/tq_1/cruces", url)
        self.assertEqual("clave", clave)

    def test_localhost_se_cambia_por_ipv4(self):
        c = ClienteVision("http://localhost:5000", "tq_1", "v-1", enviar=EnvioFalso())
        self.assertEqual("http://127.0.0.1:5000/api/torniquetes/tq_1/cruces", c.url_cruces)

    def test_reintenta_con_el_mismo_cuerpo_si_no_responde(self):  # C4: hasta 3 reintentos
        envio = EnvioFalso(SinRespuesta("timeout"), SinRespuesta("timeout"),
                           (200, {"resultado": "fraude", "alertaId": "al_1"}))
        cuerpo = {"trackId": 1, "sentido": "entrada", "horaFotograma": "x"}
        self.assertEqual("fraude", cliente(envio).enviar_cruce(cuerpo)["resultado"])
        self.assertEqual(3, len(envio.llamadas))
        self.assertTrue(all(c == cuerpo for _, c, _ in envio.llamadas))

    def test_se_rinde_despues_de_los_reintentos(self):
        envio = EnvioFalso(*[SinRespuesta("timeout")] * (REINTENTOS + 1))
        self.assertIsNone(cliente(envio).enviar_cruce({"trackId": 1, "sentido": "entrada", "horaFotograma": "x"}))
        self.assertEqual(REINTENTOS + 1, len(envio.llamadas))

    def test_un_400_no_se_reintenta(self):
        envio = EnvioFalso((400, {"error": "SentidoInvalido"}))
        self.assertIsNone(cliente(envio).enviar_cruce({"trackId": 1, "sentido": "x", "horaFotograma": "x"}))
        self.assertEqual(1, len(envio.llamadas))

    def test_un_500_si_se_reintenta(self):
        envio = EnvioFalso((500, None), (200, {"resultado": "registrado"}))
        resp = cliente(envio).enviar_cruce({"trackId": 1, "sentido": "salida", "horaFotograma": "x"})
        self.assertEqual("registrado", resp["resultado"])

    def test_anota_el_retraso_en_el_csv(self):  # RNF-06
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "cruces.csv")
            c = cliente(EnvioFalso((200, {"resultado": "consumido", "pasoId": 5})), csv_retrasos=ruta)
            c.enviar_cruce(c.cuerpo_cruce(3, "entrada", 1792953001.450))
            with open(ruta) as f:
                lineas = f.read().splitlines()
        self.assertEqual("horaFotograma,sesionVision,trackId,sentido,codigo,resultado,intentos,retrasoMs", lineas[0])
        self.assertTrue(lineas[1].startswith("2026-10-25T18:30:01.450Z,v-20261025-1,3,entrada,200,consumido,1,"))


class LatidoTests(unittest.TestCase):
    def test_latido_con_fps_y_sesion(self):
        envio = EnvioFalso((204, None))
        self.assertTrue(cliente(envio).enviar_latido(17.234))
        url, cuerpo, _ = envio.llamadas[0]
        self.assertEqual("http://api:5000/api/torniquetes/tq_1/vision/latido", url)
        self.assertEqual({"fps": 17.2, "sesionVision": "v-20261025-1"}, cuerpo)

    def test_latido_sin_respuesta(self):
        self.assertFalse(cliente(EnvioFalso(SinRespuesta("x"))).enviar_latido(10))


if __name__ == "__main__":
    unittest.main()
