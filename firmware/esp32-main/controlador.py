# Reglas de C1 (contratos de integración §2) sin hardware ni red: decide qué hacer con cada lectura, cada
# latido y el paso del tiempo. main.py le pasa lo que pasó y aplica lo que este módulo dice. Así se prueba
# en el PC (pruebas/test_controlador.py) y corre igual en la placa.
# El servidor decide; la ESP32 solo lee, reporta y obedece (arquitectura v2).
from reloj import diferencia

HEX = "0123456789ABCDEF"


def uid_valido(uid):
    """RNF-35: hexadecimal en mayúsculas, 8 caracteres (MIFARE Classic) o 14 (NTAG)."""
    return isinstance(uid, str) and len(uid) in (8, 14) and all(c in HEX for c in uid)


class Controlador:
    def __init__(self, arranque, ahora, repeticion_uid_ms=3000, cierre_sin_conexion_ms=10000):
        # RF-45 CA3: al arrancar, brazo cerrado y sirena apagada hasta que el primer latido diga otra cosa.
        self.arranque = arranque
        self.brazo_deseado = False
        self.sirena_deseada = False
        self.brazo_real = False
        self.sirena_real = False
        self.paso_id_abierto = None
        self.modo_registro = False
        self.conectado = False
        self.desafio = None   # el último que mandó el servidor, para el celular (Fase 4)
        self._repeticion_uid_ms = repeticion_uid_ms
        self._cierre_sin_conexion_ms = cierre_sin_conexion_ms
        self._contador = 0
        self._ultimo_uid = None
        self._ultimo_uid_ms = None
        # Sin ningún latido bueno, el plazo de RF-25 CA2 se cuenta desde el arranque.
        self._ultimo_latido_ok = ahora

    # Lecturas

    def lectura_nueva(self, uid, ahora):
        """
        Un UID leído por el lector. Devuelve el lecturaId para enviarlo, o None si no hay que enviar nada:
        UID inválido, o la misma tarjeta quieta sobre el lector (RF-18 CA3: el mismo UID dentro de 3 s).
        """
        if not uid_valido(uid):
            return None

        repetido = (uid == self._ultimo_uid and self._ultimo_uid_ms is not None
                    and diferencia(ahora, self._ultimo_uid_ms) < self._repeticion_uid_ms)
        # Mientras la tarjeta siga encima, cada detección corre el plazo: no se cobra dos veces por dejarla quieta.
        self._ultimo_uid = uid
        self._ultimo_uid_ms = ahora
        if repetido:
            return None
        return self.nuevo_lectura_id()

    def nuevo_lectura_id(self):
        """RF-18: arranque + contador de 4 dígitos (a1f3-0007)."""
        self._contador = (self._contador + 1) % 10000
        return "%s-%04d" % (self.arranque, self._contador)

    def resultado_lectura(self, respuesta):
        """
        La respuesta del servidor a una lectura, o None si no hubo respuesta tras el reintento (RF-25 CA1:
        sin respuesta no se abre). Devuelve un texto corto para el log.
        """
        if respuesta is None:
            return "sin respuesta: no se abre"

        accion = respuesta.get("accion")
        if accion == "abrir":
            # Se abre ya, sin esperar el latido (RNF-01: menos de 1 s), y se confirma en el siguiente (RF-22 CA4).
            self.brazo_deseado = True
            self.paso_id_abierto = respuesta.get("pasoId")
            return "abrir, paso %s, saldo %s" % (self.paso_id_abierto, respuesta.get("saldoRestante"))
        if accion == "registrada":
            return "tarjeta registrada en taquilla"
        return "denegar: %s" % respuesta.get("motivo")

    # Latidos

    def cuerpo_latido(self, fw):
        """Lo que la ESP32 aplicó de verdad (RF-45 CA4) y el paso para el que abrió (S15)."""
        return {
            "brazoReal": "abierto" if self.brazo_real else "cerrado",
            "sirenaReal": self.sirena_real,
            "pasoIdAbierto": self.paso_id_abierto if self.brazo_real else None,
            "arranque": self.arranque,
            "fw": fw,
        }

    def latido_ok(self, respuesta, ahora):
        """Aplica el estado deseado que manda el servidor (RF-45)."""
        self._ultimo_latido_ok = ahora
        self.conectado = True
        self.brazo_deseado = respuesta.get("brazoDeseado") == "abierto"
        self.sirena_deseada = bool(respuesta.get("sirenaDeseada"))
        self.modo_registro = bool(respuesta.get("modoRegistro"))
        self.desafio = respuesta.get("desafio")
        if not self.brazo_deseado:
            self.paso_id_abierto = None

    def revisar(self, ahora):
        """
        RF-25 CA2: si todos los latidos fallan durante 10 s, cierra el brazo y apaga la sirena por su cuenta.
        Devuelve True si acaba de perder la conexión.
        """
        if diferencia(ahora, self._ultimo_latido_ok) < self._cierre_sin_conexion_ms:
            return False

        perdida = self.conectado
        self.conectado = False
        self.brazo_deseado = False
        self.sirena_deseada = False
        self.modo_registro = False
        self.paso_id_abierto = None
        return perdida

    def aplicado(self, brazo, sirena):
        """main.py avisa lo que los actuadores quedaron haciendo; es lo que se reporta en el latido."""
        self.brazo_real = brazo
        self.sirena_real = sirena

    # LED de estado (C8): parpadeo = sin conexión; fijo = modo registro.

    def patron_led(self):
        if not self.conectado:
            return "parpadeo"
        return "fijo" if self.modo_registro else "apagado"
