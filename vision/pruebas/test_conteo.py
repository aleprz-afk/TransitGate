# Pruebas del conteo de cruces en el PC, sin cámara ni YOLO:
#   python -m unittest discover -s vision/pruebas
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from conteo import ContadorCruces, instante_en_cero  # noqa: E402

PASO = 0.04   # ~25 FPS


def horizontal(**kw):
    return ContadorCruces((0.0, 0.5), (1.0, 0.5), **kw)


def recorrer(c, tid, ys, x=0.5, t0=0.0):
    """Pasa el centro de tid por las alturas ys, una foto cada 40 ms; devuelve los cruces."""
    return [r for i, y in enumerate(ys) if (r := c.actualizar(tid, x, y, t0 + i * PASO))]


def sentidos(cruces):
    return [s for s, _ in cruces]


class CrucesTests(unittest.TestCase):
    def test_bajar_es_entrada_con_arriba_a_abajo(self):
        self.assertEqual(["entrada"], sentidos(recorrer(horizontal(), 1, [0.2, 0.4, 0.5, 0.6, 0.8])))

    def test_subir_es_salida(self):
        self.assertEqual(["salida"], sentidos(recorrer(horizontal(), 1, [0.8, 0.6, 0.5, 0.4, 0.2])))

    def test_abajo_a_arriba_invierte_el_sentido(self):
        c = horizontal(sentido_entrada="abajo_a_arriba")
        self.assertEqual(["entrada"], sentidos(recorrer(c, 1, [0.8, 0.5, 0.2])))

    def test_temblor_dentro_de_la_franja_no_cuenta(self):
        # El caso de la primera prueba: el centro oscila justo sobre la línea.
        ys = [0.3, 0.47, 0.53, 0.48, 0.52, 0.49, 0.54, 0.46]
        self.assertEqual([], recorrer(horizontal(margen=0.05), 1, ys))

    def test_cruza_solo_al_salir_de_la_franja(self):
        c = horizontal(margen=0.05)
        self.assertEqual([], recorrer(c, 1, [0.3, 0.45, 0.5, 0.54]))
        self.assertEqual("entrada", c.actualizar(1, 0.5, 0.56, 1.0)[0])

    def test_un_track_se_reporta_una_sola_vez(self):  # RF-26 CA3
        c = horizontal()
        self.assertEqual(["entrada"], sentidos(recorrer(c, 1, [0.2, 0.8, 0.2, 0.8])))

    def test_aparecer_ya_del_otro_lado_no_es_cruce(self):
        c = horizontal()
        self.assertEqual([], recorrer(c, 1, [0.8, 0.9]))

    def test_dos_personas_cuentan_por_separado(self):  # colado: dos entradas
        c = horizontal()
        self.assertEqual(["entrada"], sentidos(recorrer(c, 1, [0.2, 0.8])))
        self.assertEqual(["entrada"], sentidos(recorrer(c, 2, [0.25, 0.75])))

    def test_linea_inclinada(self):
        # De (0,0) a (1,1): el lado positivo (derecha al ir de p1 a p2) es abajo a la izquierda.
        c = ContadorCruces((0.0, 0.0), (1.0, 1.0))
        self.assertGreater(c.distancia(0.2, 0.8), 0)
        self.assertIsNone(c.actualizar(1, 0.8, 0.2, 0))
        self.assertEqual("entrada", c.actualizar(1, 0.2, 0.8, 0.1)[0])

    def test_olvida_el_lado_de_tracks_viejos_pero_no_los_reportados(self):
        c = horizontal(olvido_seg=5)
        c.actualizar(1, 0.5, 0.2, 0.0)
        c.olvidar_viejos(10.0)
        self.assertIsNone(c.actualizar(1, 0.5, 0.8, 10.1))   # sin lado previo: no es cruce
        recorrer(c, 2, [0.2, 0.8], t0=11)
        c.olvidar_viejos(20.0)
        self.assertEqual([], recorrer(c, 2, [0.2, 0.8], t0=21))  # ya reportado

    def test_valida_la_configuracion(self):
        with self.assertRaises(ValueError):
            horizontal(sentido_entrada="izquierda")
        with self.assertRaises(ValueError):
            ContadorCruces((0.5, 0.5), (0.5, 0.5))


class InstanteTests(unittest.TestCase):
    """El instante reportado es cuando el centro tocó la línea, no la foto que confirma."""

    def test_interpolacion_lineal(self):
        self.assertAlmostEqual(1.25, instante_en_cero(1.0, -0.1, 2.0, 0.3))
        self.assertAlmostEqual(1.0, instante_en_cero(1.0, 0.0, 2.0, 0.3))

    def test_reporta_el_toque_y_no_la_foto_de_salida_de_la_franja(self):
        # Camina a velocidad constante: toca la línea (0.5) en la foto 3 y sale de la
        # franja (0.55) recién en la foto 4. Con 0.02 por foto la línea se toca en t = 0.10.
        ys = [0.44, 0.46, 0.48, 0.50, 0.52, 0.54, 0.56]
        (sentido, instante), = recorrer(horizontal(margen=0.05), 1, ys)
        self.assertEqual("entrada", sentido)
        self.assertAlmostEqual(3 * PASO, instante, places=6)   # foto 3, no la 6 que confirma

    def test_toque_entre_dos_fotos(self):
        # Pasa de 0.45 a 0.57: toca 0.5 a 5/12 del intervalo entre las fotos 1 y 2.
        (_, instante), = recorrer(horizontal(margen=0.05), 1, [0.40, 0.45, 0.57])
        self.assertAlmostEqual(PASO + PASO * 5 / 12, instante, places=6)

    def test_con_temblor_usa_el_ultimo_toque_hacia_el_lado_nuevo(self):
        # Tiembla sobre la línea y después sigue: cuenta el último paso de arriba a abajo.
        ys = [0.40, 0.48, 0.52, 0.49, 0.51, 0.60]
        (_, instante), = recorrer(horizontal(margen=0.05), 1, ys)
        self.assertAlmostEqual(3 * PASO + PASO * 0.5, instante, places=6)   # entre 0.49 y 0.51

    def test_un_track_olvidado_no_interpola_con_muestras_viejas(self):
        # Si se pierde y reaparece, el toque sale de sus muestras nuevas, no de las de antes.
        c = horizontal(margen=0.05)
        c.actualizar(1, 0.5, 0.30, 0.0)
        c.olvidar_viejos(10.0)                   # se pierde el track
        c.actualizar(1, 0.5, 0.30, 10.0)         # reaparece arriba
        self.assertEqual("entrada", c.actualizar(1, 0.5, 0.70, 10.04)[0])
        c2 = horizontal(margen=0.05)
        c2.actualizar(1, 0.5, 0.30, 10.0)
        self.assertAlmostEqual(10.02, c2.actualizar(1, 0.5, 0.70, 10.04)[1], places=6)


if __name__ == "__main__":
    unittest.main()
