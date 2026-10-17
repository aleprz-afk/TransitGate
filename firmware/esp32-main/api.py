# Cliente HTTP mínimo para la API (Contrato API §6), con timeout en cada petición.
# urequests no tiene timeout y bloquea la lectura del PN532 (nota de RNF-01); aquí cada petición tiene un
# plazo total: 250 ms para el latido y 3 s para una lectura (C1). Usa HTTP/1.0 para que la API no responda
# por partes (chunked). Funciona igual en la placa y en el PC (para probar contra la API local).
import json
import socket

from reloj import ahora_ms, diferencia


class SinRespuesta(Exception):
    """No hubo respuesta a tiempo, o la red no está disponible."""


class Api:
    def __init__(self, servidor, torniquete_id, clave):
        # servidor: "http://192.168.137.10:5000"
        direccion = servidor.split("://", 1)[-1].rstrip("/")
        host, _, puerto = direccion.partition(":")
        self._host = host
        self._puerto = int(puerto or 80)
        self._base = "/api/torniquetes/" + torniquete_id
        self._clave = clave
        self._direccion = None   # se resuelve una vez y se reusa

    def latido(self, cuerpo, timeout_ms):
        return self._post(self._base + "/latido", cuerpo, timeout_ms)

    def lectura(self, lectura_id, uid, timeout_ms):
        return self._post(self._base + "/lecturas", {"lecturaId": lectura_id, "tipo": "uid", "valor": uid}, timeout_ms)

    def _post(self, ruta, cuerpo, timeout_ms):
        """Devuelve (estado HTTP, JSON). Lanza SinRespuesta si no hay respuesta completa dentro del plazo."""
        datos = json.dumps(cuerpo).encode()
        peticion = (
            "POST %s HTTP/1.0\r\nHost: %s:%d\r\nContent-Type: application/json\r\n"
            "Content-Length: %d\r\nX-Device-Key: %s\r\n\r\n" % (ruta, self._host, self._puerto, len(datos), self._clave)
        ).encode() + datos

        inicio = ahora_ms()
        s = None
        try:
            if self._direccion is None:
                # Solo IPv4: es lo que usa la red de la demo (en el PC, "localhost" resolvería primero a IPv6).
                self._direccion = socket.getaddrinfo(self._host, self._puerto, socket.AF_INET, socket.SOCK_STREAM)[0][-1]
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(timeout_ms / 1000)
            s.connect(self._direccion)
            s.send(peticion)

            partes = []
            while True:
                restante = timeout_ms - diferencia(ahora_ms(), inicio)
                if restante <= 0:
                    raise SinRespuesta("plazo vencido")
                s.settimeout(restante / 1000)
                parte = s.recv(1024)
                if not parte:
                    break
                partes.append(parte)
        except SinRespuesta:
            raise
        except OSError as error:   # timeout, red caída o servidor apagado
            raise SinRespuesta(str(error))
        finally:
            if s is not None:
                s.close()

        return _interpretar(b"".join(partes))


def _interpretar(respuesta):
    cabecera, _, cuerpo = respuesta.partition(b"\r\n\r\n")
    linea = cabecera.split(b"\r\n", 1)[0].split()
    if len(linea) < 2:
        raise SinRespuesta("respuesta inválida")
    estado = int(linea[1])
    try:
        return estado, json.loads(cuerpo) if cuerpo else None
    except ValueError:
        return estado, None
