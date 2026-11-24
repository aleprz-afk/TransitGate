# Firmware del ESP32 principal (contratos de integración C1 y C8). Lee tarjetas, las reporta a la API,
# manda un latido cada 300 ms y aplica lo que el servidor diga. Las reglas están en controlador.py.
import json
import os
import time

from actuadores import Brazo, Led, Sirena, SirenaTono
from api import Api, SinRespuesta
from controlador import Controlador
from red import Red
from reloj import ahora_ms, diferencia

VERSION = "0.1.0"


def cargar_config():
    try:
        with open("config.json") as archivo:
            return json.load(archivo)
    except OSError:
        print("[config] falta config.json: uso config.example.json (sin WiFi)")
        with open("config.example.json") as archivo:
            return json.load(archivo)


def crear_lector(config):
    if config.get("lector") == "pn532":
        from lector_pn532 import LectorPn532   # llega con el PN532
        pines = config["pines"]
        return LectorPn532(pines["sda"], pines["scl"])

    from lector_simulado import LectorSimulado
    print("[lector] simulado: botón BOOT o escribe un UID y Enter")
    return LectorSimulado(config.get("uidsPrueba"))


def enviar_lectura(api, lectura_id, uid, timeout_ms):
    """C1: un intento y, si no hay respuesta, un reintento con el mismo lecturaId (RF-18 CA4)."""
    for intento in (1, 2):
        try:
            estado, respuesta = api.lectura(lectura_id, uid, timeout_ms)
            if estado == 200:
                return respuesta
            print("[lectura] HTTP %d %s" % (estado, respuesta))
            return None
        except SinRespuesta as error:
            print("[lectura] intento %d sin respuesta (%s)" % (intento, error))
    return None


def main():
    config = cargar_config()
    pines = config["pines"]
    arranque = "%02x%02x" % tuple(os.urandom(2))   # cambia en cada reinicio (RF-45 CA3)

    # RF-45 CA3: brazo cerrado y sirena apagada antes de cualquier otra cosa.
    brazo = Brazo(pines["servo"])
    # "tono": zumbador pasivo directo al pin; si no, salida fija hacia el relé o el transistor.
    sirena = (SirenaTono if config.get("sirena") == "tono" else Sirena)(pines["sirena"])
    led = Led(pines["led"])

    controlador = Controlador(arranque, ahora_ms(), cierre_sin_conexion_ms=config["cierreSinConexionMs"])
    red = Red(config["wifi"]["ssid"], config["wifi"]["password"])
    api = Api(config["servidor"], config["torniqueteId"], config["deviceKey"])
    lector = crear_lector(config)
    print("[inicio] TransitGate %s, arranque %s, torniquete %s" % (VERSION, arranque, config["torniqueteId"]))

    ultimo_latido = None
    fallos_latido = 0   # seguidos; se avisa en el log al cuarto (~1 s) y al recuperarse, no en cada uno
    while True:
        ahora = ahora_ms()
        hay_red = red.revisar()
        if hay_red and api.usar_red(red.ip()):
            print("[red] API en " + api.host)

        uid = lector.leer()
        if uid:
            lectura_id = controlador.lectura_nueva(uid, ahora)
            if lectura_id:
                respuesta = enviar_lectura(api, lectura_id, uid, config["timeoutLecturaMs"]) if hay_red else None
                print("[lectura] %s %s: %s" % (lectura_id, uid, controlador.resultado_lectura(respuesta)))

        if ultimo_latido is None or diferencia(ahora, ultimo_latido) >= config["latidoMs"]:
            ultimo_latido = ahora
            if hay_red:
                try:
                    estado, respuesta = api.latido(controlador.cuerpo_latido(VERSION), config["timeoutLatidoMs"])
                    if estado == 200:
                        controlador.latido_ok(respuesta, ahora_ms())
                        if fallos_latido >= 4:
                            print("[latido] responde de nuevo tras %d fallos" % fallos_latido)
                        fallos_latido = 0
                    else:
                        print("[latido] HTTP %d %s" % (estado, respuesta))
                except SinRespuesta as error:
                    # Cuenta como fallo; a los 10 s sin ninguno, revisar() cierra el brazo.
                    fallos_latido += 1
                    if fallos_latido == 4:
                        print("[latido] sin respuesta (%s); el servidor marca fuera de línea a los 5 s" % error)

        if controlador.revisar(ahora_ms()):
            print("[conexion] 10 s sin latidos: brazo cerrado y sirena apagada (RF-25 CA2)")

        controlador.aplicado(brazo.poner(controlador.brazo_deseado), sirena.poner(controlador.sirena_deseada))
        led.mostrar(controlador.patron_led())
        time.sleep_ms(20)


try:
    main()
except KeyboardInterrupt:
    print("[fin] detenido desde el puerto serie")