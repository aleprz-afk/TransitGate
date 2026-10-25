"""Conteo de cruces sobre la línea virtual, sin cámara ni red (se prueba en el PC).

Cada persona seguida (trackId) tiene un lado confirmado de la línea. Alrededor de la
línea hay una franja: mientras el centro de la persona está dentro de ella no cambia
nada, así que el temblor del recuadro justo encima de la línea no cuenta cruces. Un
cruce es pasar de un lado confirmado al otro, atravesando la franja completa.

Un trackId se reporta una sola vez, aunque vuelva a cruzar (RF-26 CA3, C4).

El cruce se confirma al salir de la franja, unas fotos después de tocar la línea. Por eso
el instante que se reporta no es el de esa foto, sino el momento en que el centro tocó
la línea: se interpola linealmente entre las dos fotos en que la distancia a la línea
cambió de signo (C4, "Hora del cruce").
"""

SENTIDOS = ("arriba_a_abajo", "abajo_a_arriba")


class ContadorCruces:
    def __init__(self, p1, p2, sentido_entrada="arriba_a_abajo", margen=0.05, olvido_seg=5.0):
        """p1 y p2 en coordenadas normalizadas 0..1 (C3 §4.1).

        El lado positivo de la línea es el de la derecha al ir de p1 a p2 en la imagen
        (y crece hacia abajo): con p1 = (0, 0.5) y p2 = (1, 0.5), el de abajo.
        """
        if sentido_entrada not in SENTIDOS:
            raise ValueError(f"sentido_entrada debe ser uno de {SENTIDOS}")
        self.p1, self.p2 = p1, p2
        dx, dy = p2[0] - p1[0], p2[1] - p1[1]
        self._largo = (dx * dx + dy * dy) ** 0.5
        if self._largo == 0:
            raise ValueError("p1 y p2 no pueden ser el mismo punto")
        self.entrada_hacia_positivo = sentido_entrada == "arriba_a_abajo"
        self.margen = margen
        self.olvido_seg = olvido_seg
        self._lado = {}        # trackId -> -1 o +1 (último lado confirmado)
        self._visto = {}       # trackId -> último instante en que apareció
        self._muestra = {}     # trackId -> (instante, distancia) de la foto anterior
        self._toque = {}       # trackId -> instante interpolado del último paso por la línea
        self._reportados = set()

    def distancia(self, x, y):
        """Distancia con signo del punto (normalizado) a la línea."""
        (x1, y1), (x2, y2) = self.p1, self.p2
        return ((x2 - x1) * (y - y1) - (y2 - y1) * (x - x1)) / self._largo

    def actualizar(self, track_id, x, y, ahora):
        """Registra el centro (normalizado) de una persona en el instante ahora.

        Devuelve (sentido, instante) si confirma un cruce: sentido es "entrada" o "salida"
        e instante el momento interpolado en que tocó la línea. Si no, None.
        """
        self._visto[track_id] = ahora
        d = self.distancia(x, y)
        anterior = self._muestra.get(track_id)
        self._muestra[track_id] = (ahora, d)
        if anterior is not None and (anterior[1] > 0) != (d > 0):
            self._toque[track_id] = instante_en_cero(anterior[0], anterior[1], ahora, d)

        if abs(d) <= self.margen:
            return None
        lado = 1 if d > 0 else -1
        previo = self._lado.get(track_id)
        self._lado[track_id] = lado
        if previo is None or previo == lado or track_id in self._reportados:
            return None
        self._reportados.add(track_id)
        hacia_positivo = lado > 0
        sentido = "entrada" if hacia_positivo == self.entrada_hacia_positivo else "salida"
        # Sin toque registrado (el track se perdió justo al cruzar) se usa la foto actual.
        return sentido, self._toque.pop(track_id, ahora)

    def olvidar_viejos(self, ahora):
        """Suelta el lado de los trackId que no aparecen hace rato (los reportados se recuerdan)."""
        for tid in [t for t, v in self._visto.items() if ahora - v > self.olvido_seg]:
            del self._visto[tid]
            self._lado.pop(tid, None)
            self._muestra.pop(tid, None)
            self._toque.pop(tid, None)

    def reiniciar(self):
        self._lado.clear()
        self._visto.clear()
        self._muestra.clear()
        self._toque.clear()
        self._reportados.clear()


def instante_en_cero(t0, d0, t1, d1):
    """Interpolación lineal: instante entre t0 y t1 en que la distancia pasa por cero.

    Las fotos llegan cada ~40 ms y una persona camina casi recto en ese tramo, así que
    la recta basta: el error es de pocos ms frente a la ventana de 10 s de S4.
    """
    if d0 == d1:
        return t1
    return t0 + (t1 - t0) * d0 / (d0 - d1)
