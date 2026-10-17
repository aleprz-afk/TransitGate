import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// La API corre en el puerto 5000. Para usar la de otra laptop: API_URL=http://<IP>:5000 npm run dev
const api = process.env.API_URL ?? 'http://localhost:5000'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // En desarrollo, Vite reenvía a la API las rutas del backend; así el navegador ve un solo origen
    // y no hace falta CORS, igual que en la demo (Contratos de integración, C6).
    proxy: {
      '/api': api,
      '/hubs': { target: api, ws: true },
      '/openapi': api,
      '/scalar': api,
    },
  },
  build: {
    // Para la demo, la API sirve la web desde su wwwroot en el mismo puerto.
    outDir: '../server/TransitGate.Api/wwwroot',
    emptyOutDir: true,
  },
})
