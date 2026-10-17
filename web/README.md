# web

Portal del cliente y panel de operación en React (Vite + TypeScript). Dueña: Ana.

Por ahora hay una página de prueba: `src/App.tsx` pide iniciar sesión y, según el rol, muestra el panel (`src/Panel.tsx`: torniquetes, alertas abiertas e intentos en vivo) o el saldo del cliente. Sirve para comprobar que el proxy, la sesión, los tipos y SignalR funcionan, y se reemplaza por las páginas reales.

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

- `src/api/cliente.ts`: cliente HTTP tipado (`openapi-fetch`). Ejemplo: `api.GET('/api/torniquetes')`.
- `src/api/eventos.ts`: conexión al hub `/hubs/eventos` y tipos de sus eventos. El OpenAPI no describe SignalR, así que estos tipos van a mano y siguen el contrato C6.
- `src/hora.ts`: las horas llegan en UTC y se muestran en hora de Colombia (RNF-42).

## Demo

```
npm run build
```

Deja la web compilada en `server/TransitGate.Api/wwwroot` (no se sube al repositorio). Al arrancar, la API la sirve en el mismo puerto, `http://<laptop>:5000`; cualquier ruta que no sea de la API devuelve `index.html`. Si la API ya estaba corriendo cuando se creó `wwwroot` por primera vez, hay que reiniciarla.
