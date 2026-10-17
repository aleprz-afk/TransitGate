import createClient from 'openapi-fetch'
import type { components, paths } from './esquema'

// Cliente HTTP tipado con el OpenAPI de la API (npm run tipos regenera esquema.d.ts).
// En desarrollo el proxy de Vite reenvía /api, y en la demo la API sirve la web: siempre el mismo origen,
// así la cookie de sesión viaja sola.
export const api = createClient<paths>({ baseUrl: window.location.origin })

// Si la sesión vence (60 min sin uso, RNF-18), cualquier ruta protegida responde 401: la web vuelve a pedirla.
// El 401 del login no cuenta: ahí significa credenciales malas.
let alPerderSesion: () => void = () => {}
export function avisarSesionPerdida(accion: () => void) {
  alPerderSesion = accion
}
api.use({
  onResponse({ request, response }) {
    if (response.status === 401 && !request.url.includes('/api/auth/')) alPerderSesion()
  },
})

export type Esquema = components['schemas']
export type TorniqueteResumen = Esquema['TorniqueteResumen']
export type IntentoResumen = Esquema['IntentoResumen']
export type AlertaResumen = Esquema['AlertaResumen']
export type Sesion = Esquema['RespuestaSesion']

/** Error de la API: código estable más mensaje en español claro (Contrato §1, RNF-32). */
export interface ErrorApi {
  error: string
  mensaje: string
}

/** El mensaje para mostrar de un error de openapi-fetch, o uno genérico si la API no respondió con ErrorApi. */
export function mensajeDe(error: unknown, generico = 'Algo falló. Intenta de nuevo.'): string {
  return (error as Partial<ErrorApi> | undefined)?.mensaje ?? generico
}

export function codigoDe(error: unknown): string | undefined {
  return (error as Partial<ErrorApi> | undefined)?.error
}
