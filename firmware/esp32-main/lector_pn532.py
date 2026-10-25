# Lector PN532 por I2C (C8: SDA en GPIO 21, SCL en GPIO 22, dirección 0x24; interruptores del módulo en I2C).
# Misma interfaz que LectorSimulado: leer() devuelve el UID en hexadecimal mayúsculas o None.
#
# leer() nunca bloquea el bucle de main.py (RNF-01): manda la búsqueda de tarjeta (InListPassiveTarget) y en
# cada llamada solo pregunta si el PN532 ya respondió. Si el lector falla o se desconecta, leer() devuelve
# None y vuelve a iniciarlo cada 2 s; el latido y el resto del firmware siguen funcionando.
# La tarjeta quieta sobre el lector se reporta en cada búsqueda: controlador.py descarta las repetidas (RF-18 CA3).
from reloj import ahora_ms, diferencia

DIRECCION = 0x24

HOST_A_PN532 = 0xD4
PN532_A_HOST = 0xD5
CMD_FIRMWARE = 0x02          # GetFirmwareVersion
CMD_SAM = 0x14               # SAMConfiguration
CMD_RF = 0x32                # RFConfiguration
CMD_BUSCAR = 0x4A            # InListPassiveTarget
ACK = b"\x00\x00\xff\x00\xff\x00"

# Reintentos de la búsqueda dentro del PN532: con un número finito responde "sin tarjeta" cada poco,
# y así se nota si deja de responder (con 0xFF esperaría para siempre).
REINTENTOS_BUSQUEDA = 0x10
PLAZO_COMANDO_MS = 500       # al iniciar, lo que puede tardar cada respuesta
DESPERTAR_MS = 50           # el PN532 tarda en despertar; antes de eso no contesta (ENODEV)
INTENTOS_COMANDO = 5        # al iniciar, si no contesta se reintenta cada 20 ms
PLAZO_RESPUESTA_MS = 3000
REINICIO_MS = 2000
LARGO_RESPUESTA = 32         # cabe la respuesta de una tarjeta con UID de hasta 10 bytes


def trama(comando, datos=b""):
    """Trama normal del PN532: 00 00 FF LEN LCS D4 CMD datos DCS 00."""
    cuerpo = bytes([HOST_A_PN532, comando]) + bytes(datos)
    largo = len(cuerpo)
    return (b"\x00\x00\xff" + bytes([largo, (0x100 - largo) & 0xFF]) + cuerpo
            + bytes([(0x100 - sum(cuerpo)) & 0xFF, 0x00]))


def datos_respuesta(trama_leida, comando):
    """
    Los datos de la respuesta a `comando` (sin D5 ni el código), o None si la trama no es válida:
    sin encabezado, con suma de verificación errada o de otro comando.
    """
    i = trama_leida.find(b"\x00\xff")
    if i < 0 or i + 4 > len(trama_leida):
        return None
    largo, lcs = trama_leida[i + 2], trama_leida[i + 3]
    if largo < 2 or (largo + lcs) & 0xFF:
        return None
    inicio = i + 4
    if inicio + largo >= len(trama_leida):
        return None
    cuerpo = trama_leida[inicio:inicio + largo]
    if (sum(cuerpo) + trama_leida[inicio + largo]) & 0xFF:
        return None
    if cuerpo[0] != PN532_A_HOST or cuerpo[1] != comando + 1:
        return None
    return cuerpo[2:]


def uid_de_busqueda(datos):
    """
    UID de la respuesta de InListPassiveTarget (tipo A, 106 kbps), en hexadecimal mayúsculas, o None sin tarjeta.
    Datos: NbTg, Tg, SENS_RES (2), SEL_RES, NFCIDLength, NFCID...
    """
    if not datos or datos[0] == 0 or len(datos) < 6:
        return None
    largo = datos[5]
    uid = datos[6:6 + largo]
    if largo == 0 or len(uid) < largo:
        return None
    return "".join("%02X" % b for b in uid)


class LectorPn532:
    def __init__(self, sda, scl, i2c=None, direccion=DIRECCION, reloj=ahora_ms, dormir_ms=None):
        if i2c is None:
            # SoftI2C: el PN532 estira el reloj y el I2C por hardware del ESP32 clásico suele cortar la lectura.
            from machine import Pin, SoftI2C
            i2c = SoftI2C(scl=Pin(scl), sda=Pin(sda), freq=50_000, timeout=10_000)
        if dormir_ms is None:
            import time
            dormir_ms = getattr(time, "sleep_ms", lambda ms: time.sleep(ms / 1000))
        self._i2c = i2c
        self._direccion = direccion
        self._reloj = reloj
        self._dormir_ms = dormir_ms
        self._listo = False
        self._esperando = None        # None, "ack" o "respuesta"
        self._enviado_ms = None
        self._ultimo_intento_ms = None
        self._avisado = False         # para escribir una línea por caída, no una por intento
        self.version = None
        self._iniciar()

    # Interfaz del lector

    def leer(self):
        ahora = self._reloj()
        if not self._listo:
            if diferencia(ahora, self._ultimo_intento_ms) >= REINICIO_MS:
                self._iniciar()
            return None
        try:
            return self._avanzar(ahora)
        except OSError as error:
            self._falla("se perdió la conexión (%s)" % error)
            return None

    # Búsqueda sin bloquear

    def _avanzar(self, ahora):
        if self._esperando is None:
            self._enviar_busqueda(ahora)
            return None

        if not self._preparado():
            # Sin ACK visible, el PN532 queda ocupado hasta tener la respuesta: el plazo es el de la búsqueda.
            if diferencia(ahora, self._enviado_ms) > PLAZO_RESPUESTA_MS:
                self._falla("no responde")
            return None

        leida = self._leer_trama(LARGO_RESPUESTA)
        if self._esperando == "ack" and leida.startswith(ACK):
            self._esperando = "respuesta"
            return None

        # A veces el PN532 no deja ver el ACK y entrega la respuesta de una vez: también vale.
        datos = datos_respuesta(leida, CMD_BUSCAR)
        if datos is None:
            self._enviar_busqueda(ahora)
            return None
        self._esperando = None
        return uid_de_busqueda(datos)

    def _enviar_busqueda(self, ahora):
        # Una tarjeta, tipo A a 106 kbps (MIFARE y NTAG).
        self._escribir(trama(CMD_BUSCAR, b"\x01\x00"))
        self._esperando = "ack"
        self._enviado_ms = ahora

    # Inicio

    def _iniciar(self):
        self._ultimo_intento_ms = self._reloj()
        self._esperando = None
        try:
            try:
                self._escribir(b"\x00")   # lo despierta; la primera vez suele no contestar
            except OSError:
                pass
            self._dormir_ms(DESPERTAR_MS)
            firmware = self._comando(CMD_FIRMWARE)
            if not firmware or len(firmware) < 3 or firmware[0] != 0x32:
                raise OSError("respuesta inesperada a GetFirmwareVersion")
            self.version = "%d.%d" % (firmware[1], firmware[2])
            self._comando(CMD_SAM, b"\x01\x14\x01")                                  # modo normal
            self._comando(CMD_RF, bytes([0x05, 0xFF, 0x01, REINTENTOS_BUSQUEDA]))    # reintentos
        except OSError as error:
            self._falla("no aparece en 0x%02X (%s)" % (self._direccion, error))
            return
        self._listo = True
        self._avisado = False
        print("[lector] PN532 listo, firmware %s" % self.version)

    def _comando(self, comando, datos=b""):
        """Solo al iniciar: manda el comando y espera el ACK y la respuesta (bloquea unos ms)."""
        for intento in range(INTENTOS_COMANDO):
            try:
                self._escribir(trama(comando, datos))
                break
            except OSError:
                if intento == INTENTOS_COMANDO - 1:
                    raise
                self._dormir_ms(20)
        self._esperar_preparado(PLAZO_COMANDO_MS)
        leida = self._leer_trama(LARGO_RESPUESTA)
        if leida.startswith(ACK):
            self._esperar_preparado(PLAZO_COMANDO_MS)
            leida = self._leer_trama(LARGO_RESPUESTA)
        # Si no hubo ACK, lo leído ya es la respuesta.
        respuesta = datos_respuesta(leida, comando)
        if respuesta is None:
            raise OSError("respuesta inválida")
        return respuesta

    def _esperar_preparado(self, plazo_ms):
        inicio = self._reloj()
        while not self._preparado():
            if diferencia(self._reloj(), inicio) > plazo_ms:
                raise OSError("no responde")
            self._dormir_ms(1)

    def _falla(self, motivo):
        if not self._avisado:
            print("[lector] PN532 %s; reintento cada %d s" % (motivo, REINICIO_MS // 1000))
            self._avisado = True
        self._listo = False
        self._esperando = None
        self._ultimo_intento_ms = self._reloj()

    # I2C: cada lectura del PN532 empieza con un byte de estado (bit 0 = listo).

    def _preparado(self):
        # Mientras procesa una orden, el PN532 no contesta a su dirección (ENODEV): eso es "todavía no".
        # Si de verdad se desconectó, lo detectan los plazos de cada espera.
        try:
            return bool(self._i2c.readfrom(self._direccion, 1)[0] & 0x01)
        except OSError:
            return False

    def _leer_trama(self, largo):
        return bytes(self._i2c.readfrom(self._direccion, largo + 1)[1:])

    def _escribir(self, datos):
        self._i2c.writeto(self._direccion, datos)
