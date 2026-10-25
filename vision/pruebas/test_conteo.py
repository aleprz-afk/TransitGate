# Pruebas del conteo de cruces en el PC, sin cámara ni YOLO:
#   python -m unittest discover -s vision/pruebas
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from conteo import ContadorCruces  # noqa: E402


def horizontal(**kw):
    return ContadorCruces((0.0, 0.5), (1.0, 0.5), **kw)


def recorrer(c, tid, ys, x=0.5, t0=0.0):
    """Pasa el centro de tid por las alturas ys, un cuadro cada 50 ms; devuelve los cruces."""
    return [s for i, y in enumerate(ys) if (s := c.actualizar(tid, x, y, t0 + i * 0.05))]


class CrucesTests(unittest.TestCase):
    def test_bajar_es_entrada_con_arriba_a_abajo(self):
        self.assertEqual(["entrada"], recorrer(horizontal(), 1, [0.2, 0.4, 0.5, 0.6, 0.8]))

    def test_subir_es_salida(self):
        self.assertEqual(["salida"], recorrer(horizontal(), 1, [0.8, 0.6, 0.5, 0.4, 0.2]))

    def test_abajo_a_arriba_invierte_el_sentido(self):
        c = horizontal(sentido_entrada="abajo_a_arriba")
        self.assertEqual(["entrada"], recorrer(c, 1, [0.8, 0.5, 0.2]))

    def test_temblor_dentro_de_la_franja_no_cuenta(self):
        # El caso de la primera prueba: el centro oscila justo sobre la línea.
        ys = [0.3, 0.47, 0.53, 0.48, 0.52, 0.49, 0.54, 0.46]
        self.assertEqual([], recorrer(horizontal(margen=0.05), 1, ys))

    def test_cruza_solo_al_salir_de_la_franja(self):
        c = horizontal(margen=0.05)
        self.assertEqual([], recorrer(c, 1, [0.3, 0.45, 0.5, 0.54]))
        self.assertEqual("entrada", c.actualizar(1, 0.5, 0.56, 1.0))

    def test_un_track_se_reporta_una_sola_vez(self):  # RF-26 CA3
        c = horizontal()
        self.assertEqual(["entrada"], recorrer(c, 1, [0.2, 0.8, 0.2, 0.8]))

    def test_aparecer_ya_del_otro_lado_no_es_cruce(self):
        c = horizontal()
        self.assertEqual([], recorrer(c, 1, [0.8, 0.9]))

    def test_dos_personas_cuentan_por_separado(self):  # colado: dos entradas
        c = horizontal()
        self.assertEqual(["entrada"], recorrer(c, 1, [0.2, 0.8]))
        self.assertEqual(["entrada"], recorrer(c, 2, [0.25, 0.75]))

    def test_linea_inclinada(self):
        # De (0,0) a (1,1): el lado positivo (derecha al ir de p1 a p2) es abajo a la izquierda.
        c = ContadorCruces((0.0, 0.0), (1.0, 1.0))
        self.assertGreater(c.distancia(0.2, 0.8), 0)
        self.assertEqual("entrada", c.actualizar(1, 0.8, 0.2, 0) or c.actualizar(1, 0.2, 0.8, 0.1))

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


if __name__ == "__main__":
    unittest.main()
