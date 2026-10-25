# Pruebas de la separación de JPEG del MJPEG, sin red ni cámara.
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from stream import separar_jpegs  # noqa: E402

J1 = b"\xff\xd8uno\xff\xd9"
J2 = b"\xff\xd8dos\xff\xd9"
SEP = b"\r\n--transitgate-cuadro\r\nContent-Type: image/jpeg\r\nContent-Length: 7\r\n\r\n"


class SepararTests(unittest.TestCase):
    def test_saca_los_jpeg_completos_y_deja_el_incompleto(self):
        jpegs, resto = separar_jpegs(SEP + J1 + SEP + J2 + SEP + b"\xff\xd8med")
        self.assertEqual([J1, J2], jpegs)
        self.assertEqual(b"\xff\xd8med", resto)

    def test_un_jpeg_partido_en_dos_lecturas(self):
        datos = SEP + J1
        jpegs, resto = separar_jpegs(datos[:-4])
        self.assertEqual([], jpegs)
        jpegs, _ = separar_jpegs(resto + datos[-4:])
        self.assertEqual([J1], jpegs)

    def test_marcador_de_inicio_partido(self):
        jpegs, resto = separar_jpegs(SEP + b"\xff")
        self.assertEqual([], jpegs)
        jpegs, _ = separar_jpegs(resto + J1[1:])
        self.assertEqual([J1], jpegs)

    def test_sin_jpeg_no_acumula_las_cabeceras(self):
        jpegs, resto = separar_jpegs(SEP * 50)
        self.assertEqual([], jpegs)
        self.assertLessEqual(len(resto), 1)


if __name__ == "__main__":
    unittest.main()
