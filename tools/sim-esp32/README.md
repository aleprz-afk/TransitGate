# sim-esp32

Simulador del ESP32 principal (RNF-38). Dueña: Karol.

Corre en el PC y le habla a la API igual que la placa: usa `controlador.py` y `api.py` de `firmware/esp32-main`, así que sigue exactamente las mismas reglas de C1 (latido cada 300 ms con 250 ms de plazo, lecturas con un reintento, tarjeta quieta, cierre a los 10 s sin conexión). El brazo y la sirena se muestran en la consola.

Con este simulador, `sim-vision` y la API se puede hacer el flujo completo sin hardware (contratos de integración §11). También sirve para el H2.

## Uso

Solo necesita Python 3, sin instalar nada. Desde la raíz del repo, con la API arriba:

```
python tools/sim-esp32/sim_esp32.py
```

Opciones: `--servidor http://<IP>:5000` (por defecto `http://localhost:5000`) y `--torniquete tq_1`. Si la API está en modo estricto, la clave del ESP32 va en la variable de entorno `SIM_ESP32_CLAVE`.

## Comandos

| Comando | Qué hace |
| --- | --- |
| `tarjeta <uid>` | Acerca una tarjeta (UID en hex, 8 o 14 caracteres). La misma tarjeta dentro de 3 s se ignora, como en la placa (RF-18 CA3) |
| `celular <soporteId> <claveHex>` | Acerca un celular: firma el desafío del último latido como lo haría la app (C2 §3.3). La API lo rechaza hasta la Fase 4 |
| `reiniciar` | Reinicia la ESP32: brazo cerrado, sirena apagada y un `arranque` nuevo (RF-45 CA3) |
| `cortar-red <s>` | Sin red durante `s` segundos: a los 10 s cierra el brazo por su cuenta (RF-25 CA2) |
| `no-confirmar` | Activa o desactiva: el brazo "no abre" aunque el servidor lo pida, y el latido no confirma el paso (RF-46) |
| `estado` | Muestra el estado actual |
| `salir` | Termina |

## Tarjetas de prueba

Las de `DatosDePrueba` (ver `server/README.md`): `04A1B2C3` (activa con saldo), `DEADBEEF` (saldo insuficiente), `0BADCAFE` (bloqueada) y `5011C17A` (entregada, sin activar). En modo registro (taquilla), cualquier UID nuevo, por ejemplo `C0FFEE01`, queda ligado a la solicitud.
