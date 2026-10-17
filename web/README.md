# web

Portal del cliente y panel de operación en React (Vite + TypeScript, React Router). Pensada primero para el celular (360 px, RNF-31), con modo oscuro.

| Ruta | Quién | Qué hace |
| --- | --- | --- |
| `/entrar`, `/registro` | Todos | Inicio de sesión (RF-02) y registro con la autorización de datos (RF-01, RNF-28) |
| `/mi-tarjeta` | Cliente | Saldo en vivo, estado de la tarjeta, pedir la física (RF-08), confirmar la recepción (RF-11) y el último viaje (RF-16) |
| `/recargar` | Cliente | Recarga con pago simulado, montos sugeridos y `Idempotency-Key` (RF-15) |
| `/viajes`, `/recargas` | Cliente | Historiales, el más reciente primero (RF-39, RF-17) |
| `/perfil` | Cliente | Datos y cambio de teléfono (RF-05) |
| `/panel` | Personal | Torniquetes, conexión de los dispositivos, alertas con "Apagar alarma" e intentos en vivo (RF-31 a RF-34, RF-37) |
| `/taquilla` | Personal | Solicitudes pendientes, búsqueda por documento y entrega con el modo registro (RF-36, RF-09, RF-10) |

Cada ruta pide su rol; la API lo vuelve a comprobar (RF-06). `/` lleva a cada uno a su inicio.

## Desarrollo

Primero la API, que necesita la base de datos en Docker (ver "Base de datos" en `server/README.md`). Desde la raíz del repo, o con Visual Studio:

```
dotnet run --project server/TransitGate.Api
```

Luego, en `web/`:

```
npm install
npm run dev
```

La web abre en `http://localhost:5173`. Vite reenvía a la API (`http://localhost:5000`) las rutas `/api`, `/hubs` (con WebSocket), `/openapi` y `/scalar`, así que el navegador ve un solo origen y no hace falta CORS (Contratos de integración, C6). Para usar la API de otra laptop:

```
API_URL=http://<IP>:5000 npm run dev
```

## Sesión

La API usa la cookie de sesión de Identity, en el mismo origen que la web, así que `fetch` y SignalR la mandan solos. Sin sesión, la API responde 401 y la web lleva al usuario a iniciar sesión; nunca hay redirecciones del servidor. Al recargar la página, `GET /api/auth/sesion` dice quién está (`rol` y `nombre`) o responde 401.

Para el panel: el correo y la contraseña del operador están en los secretos de usuario de la laptop que corre la API (`Semilla:Operador:*`); pídeselos a Alex. Para el portal, registra un cliente desde la web o con `POST /api/clientes`.

## Tipos de la API

`src/api/esquema.d.ts` se genera desde el OpenAPI de la API y se sube al repositorio, así se puede trabajar sin la API corriendo. Cuando cambien las rutas, con la API arriba:

```
npm run tipos
```

## Estructura

- `src/api/cliente.ts`: cliente HTTP tipado (`openapi-fetch`). Si una ruta responde 401 (la sesión venció), la web vuelve a `/entrar`. `mensajeDe(error)` saca el mensaje en español del cuerpo de error.
- `src/api/eventos.ts`: tipos de los eventos del hub. El OpenAPI no describe SignalR, así que van a mano y siguen el contrato C6.
- `src/sesion.tsx`: la sesión (`useSesion`): quién está, entrar y salir.
- `src/tiempoReal.tsx`: una sola conexión al hub mientras hay sesión. Cada página escucha lo suyo con `useEvento('SaldoActualizado', e => …)`.
- `src/textos.ts`: horas en hora de Colombia (RNF-42), pesos y los códigos de la API en español claro (motivos, estados; RNF-32).
- `src/paginas/`: una página por ruta, en `cliente/` y `personal/`.

En la demo, el celular entra por `http://<IP>:5000`, que no es un contexto seguro: `crypto.randomUUID` no existe ahí. Por eso la clave de idempotencia sale de `crypto.getRandomValues`.

## Demo

```
npm run build
```

Deja la web compilada en `server/TransitGate.Api/wwwroot` (no se sube al repositorio). Al arrancar, la API la sirve en el mismo puerto, `http://<laptop>:5000`; cualquier ruta que no sea de la API devuelve `index.html`. Si la API ya estaba corriendo cuando se creó `wwwroot` por primera vez, hay que reiniciarla.
