import createClient from 'openapi-fetch'
import type { components, paths } from './esquema'

// Cliente HTTP tipado con el OpenAPI de la API (npm run tipos regenera esquema.d.ts).
// Sin baseUrl: en desarrollo el proxy de Vite reenvía /api, y en la demo la API sirve la web.
export const api = createClient<paths>({ baseUrl: window.location.origin })

export type TorniqueteResumen = components['schemas']['TorniqueteResumen']
export type IntentoResumen = components['schemas']['IntentoResumen']
export type AlertaResumen = components['schemas']['AlertaResumen']
