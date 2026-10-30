# server

Servidor ASP.NET Core (API, dominio, base de datos). Dueño: Alex (validación y cuentas; Jeisy revisa cuentas).

## Base de datos (una sola vez por laptop)

La API guarda todo en PostgreSQL, que corre en Docker en la misma laptop (decisión del 13 de octubre en [CAMBIOS.md](CAMBIOS.md)). Requisito: Docker Desktop instalado y **abierto**. Desde la raíz del repo:

1. Copia `.env.example` a `.env` y pon una contraseña propia (`.env` no se sube al repositorio).
2. Levanta la base: `docker compose up -d`. Queda corriendo y arranca sola con Docker Desktop. Para detenerla: `docker compose down`. **`docker compose down -v` borra todos los datos.**
3. Guarda la cadena de conexión y las cuentas del personal en los secretos de usuario (RNF-25), con la misma contraseña del `.env`:

```
dotnet user-secrets set "ConnectionStrings:TransitGate" "Host=localhost;Port=5432;Database=transitgate;Username=transitgate;Password=<la del .env>" --project server/TransitGate.Api
dotnet user-secrets set "Semilla:Operador:Correo" "operador@transitgate.test" --project server/TransitGate.Api
dotnet user-secrets set "Semilla:Operador:Password" "<al menos 8 caracteres>" --project server/TransitGate.Api
dotnet user-secrets set "Semilla:Administrador:Correo" "admin@transitgate.test" --project server/TransitGate.Api
dotnet user-secrets set "Semilla:Administrador:Password" "<al menos 8 caracteres>" --project server/TransitGate.Api
dotnet user-secrets set "Semilla:Seguridad:Correo" "seguridad@transitgate.test" --project server/TransitGate.Api
dotnet user-secrets set "Semilla:Seguridad:Password" "<al menos 8 caracteres>" --project server/TransitGate.Api
```

Al arrancar, la API aplica las migraciones, crea los roles y las cuentas del personal que falten y, si la base no tiene clientes, carga los de prueba. Sin la cadena de conexión, la API no arranca y dice qué falta.

Para crear una migración nueva (desde la raíz del repo): `dotnet tool restore` y luego `dotnet ef migrations add <Nombre> --project server/TransitGate.Infrastructure --startup-project server/TransitGate.Api --output-dir Persistencia/Migraciones`.

## Ejecutar

```
dotnet run --project TransitGate.Api
```

La API escucha en `http://0.0.0.0:5000` (contratos de integración §9), así que el ESP32 y la visión la alcanzan por la IP de la laptop. La primera vez Windows pregunta por el firewall: permitir en red **Privada**.

- Web (después de `npm run build` en `web/`): `http://localhost:5000/`
- Salud: `http://localhost:5000/api/salud`
- Documentación (Scalar): `http://localhost:5000/scalar`
- Pruebas: `dotnet test`. Las de `TransitGate.Api.Tests` levantan su propio PostgreSQL en un contenedor, así que también necesitan Docker Desktop abierto.

## Estado actual (semana 2)

Las rutas de los dispositivos siguen el Contrato API (§6 y §7) y pasan por los casos de uso de Application; el módulo de validación habla con el de cuentas por las interfaces de C7. Todo se guarda en PostgreSQL, salvo el estado de cada torniquete (`Cerrado`, `EsperandoCruce`…), que vive en memoria y arranca en `Cerrado` al reiniciar. Las rutas del ESP32 y de la visión verifican `X-Device-Key` en modo tolerante (ver "Credenciales de los dispositivos" en [CAMBIOS.md](CAMBIOS.md)). El panel, la taquilla y el portal piden sesión (cookie de Identity).

| Ruta | Qué hace hoy |
| --- | --- |
| `POST /api/torniquetes/{id}/lecturas` | Valida contra las tarjetas de prueba, debita la tarifa y abre un paso; repite la respuesta si llega el mismo `lecturaId` |
| `POST /api/torniquetes/{id}/latido` | Registra la señal de vida y confirma la apertura (`pasoIdAbierto`); devuelve el estado deseado y un desafío fijo |
| `POST /api/torniquetes/{id}/cruces` | Consume el paso, marca fraude o responde `duplicado` |
| `POST /api/torniquetes/{id}/vision/latido` | Registra la señal de vida de la visión; responde 204 |
| `GET /api/torniquetes` | Estado y conexión de cada torniquete (personal) |
| `GET /api/torniquetes/{id}/intentos` | Últimos 100 intentos de acceso (personal) |
| Hub `/hubs/eventos` | Eventos en vivo: el personal en el grupo `operacion`, cada cliente en el suyo (con sesión) |
| `GET /api/alertas?estado=` | Alertas de colado, opcionalmente por estado (personal) |
| `POST /api/alertas/{id}/apagar-alarma` | Apaga la alarma y deja la alerta Atendida (personal) |
| `GET /api/tarjetas/solicitudes?documento=` | Solicitudes de tarjeta física pendientes, la más antigua primero (personal) |
| `POST /api/torniquetes/{id}/modo-registro` | Pone el lector en modo registro para entregar la tarjeta de una solicitud (personal) |
| `POST /api/auth/login`, `/logout`, `GET /api/auth/sesion` | Sesión con cookie; 6 intentos fallidos bloquean 5 min |
| `POST /api/clientes`, `GET`/`PATCH /api/clientes/me` | Registro (crea la tarjeta e inicia sesión) y perfil del cliente |
| `/api/tarjetas/mi-tarjeta` y `/solicitud-fisica`, `/confirmar-recepcion`, `/recargas`, `/viajes` | Portal del cliente: saldo, tarjeta física, recargas e historial (cliente) |
| Cualquier otra ruta | La web compilada en `wwwroot` (`index.html`); una ruta desconocida bajo `/api` responde 404 |

Tarjetas de prueba (sección `DatosDePrueba` de `appsettings.json`; se cargan en la base la primera vez, cuando no hay clientes, cada una como un cliente con su tarjeta física). La tarifa (3.200) y los montos de recarga están en la sección `Cuentas`:

| UID | Estado | Saldo | Respuesta |
| --- | --- | --- | --- |
| `04A1B2C3` | Activa | 12.000 | Abre tres veces y luego `SaldoInsuficiente` |
| `DEADBEEF` | Activa | 2.000 | `SaldoInsuficiente` |
| `0BADCAFE` | Bloqueada | 50.000 | `SoporteBloqueado` |
| `5011C17A` | Entregada | 0 | `SoporteNoActivo` |
| cualquier otro | — | — | `TarjetaNoRegistrada` |

Solicitudes de prueba (sección `Solicitudes`): Luisa Rojas (documento `1032456789`, saldo 10.000) y Mateo Díaz (`1121987654`, saldo 0). Al ligarles un UID en modo registro, su tarjeta queda `Entregada` y responde `SoporteNoActivo` hasta que el cliente confirme la recepción.

Para probar con una tarjeta real, lo más simple es registrarla como lo haría un cliente: registro en el portal, solicitud, modo registro en taquilla y confirmación. Los tiempos (expiración del paso, gracia de visión y plazo de conexión del ESP32) están en la sección `Operacion`.

## Mediciones

El log sale una línea por evento con hora UTC al milisegundo. Las líneas que empiezan con `RNF-04` registran cada cambio del estado deseado y las que empiezan con `RNF-06`, cada cruce con su retraso. Para guardar el log de una sesión de pruebas (PowerShell):

```
dotnet run --project TransitGate.Api | Tee-Object -FilePath mediciones.log
```

Lo que cambió y lo que necesita saber cada integrante está en [CAMBIOS.md](CAMBIOS.md).
