"""Conteo de cruces sobre la línea virtual, sin cámara ni red (se prueba en el PC).

Cada persona seguida (trackId) tiene un lado confirmado de la línea. Alrededor de la
línea hay una franja: mientras el centro de la persona está dentro de ella no cambia
nada, así que el temblor del recuadro justo encima de la línea no cuenta cruces. Un
cruce es pasar de un lado confirmado al otro, atravesando la franja completa.

Un trackId se reporta una sola vez, aunque vuelva a cruzar (RF-26 CA3, C4).
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
        self._reportados = set()

    def distancia(self, x, y):
        """Distancia con signo del punto (normalizado) a la línea."""
        (x1, y1), (x2, y2) = self.p1, self.p2
        return ((x2 - x1) * (y - y1) - (y2 - y1) * (x - x1)) / self._largo

    def actualizar(self, track_id, x, y, ahora):
        """Registra el centro (normalizado) de una persona. Devuelve "entrada", "salida" o None."""
        self._visto[track_id] = ahora
        d = self.distancia(x, y)
        if abs(d) <= self.margen:
            return None
        lado = 1 if d > 0 else -1
        previo = self._lado.get(track_id)
        self._lado[track_id] = lado
        if previo is None or previo == lado or track_id in self._reportados:
            return None
        self._reportados.add(track_id)
        hacia_positivo = lado > 0
        return "entrada" if hacia_positivo == self.entrada_hacia_positivo else "salida"

    def olvidar_viejos(self, ahora):
        """Suelta el lado de los trackId que no aparecen hace rato (los reportados se recuerdan)."""
        for tid in [t for t, v in self._visto.items() if ahora - v > self.olvido_seg]:
            del self._visto[tid]
            self._lado.pop(tid, None)

    def reiniciar(self):
        self._lado.clear()
        self._visto.clear()
        self._reportados.clear()
