import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, avisarSesionPerdida, type Sesion } from './api/cliente'

// La sesión es la cookie de Identity (Contrato §2). Al cargar la página se pregunta quién está;
// si cualquier ruta responde 401 (la sesión venció), se vuelve a pedir el inicio de sesión.

interface ContextoSesion {
  /** undefined: todavía no se sabe; null: sin sesión. */
  sesion: Sesion | null | undefined
  entrar: (sesion: Sesion) => void
  salir: () => Promise<void>
}

const Contexto = createContext<ContextoSesion>({ sesion: undefined, entrar: () => {}, salir: async () => {} })

export function SesionProvider({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<Sesion | null | undefined>(undefined)

  useEffect(() => {
    avisarSesionPerdida(() => setSesion(null))
    api.GET('/api/auth/sesion').then(({ data }) => setSesion(data ?? null))
  }, [])

  async function salir() {
    await api.POST('/api/auth/logout')
    setSesion(null)
  }

  return <Contexto.Provider value={{ sesion, entrar: setSesion, salir }}>{children}</Contexto.Provider>
}

export function useSesion() {
  return useContext(Contexto)
}

export const esPersonal = (sesion: Sesion | null | undefined) =>
  sesion?.rol === 'Operador' || sesion?.rol === 'Seguridad' || sesion?.rol === 'Administrador'

// Lo que puede hacer cada rol del personal (RF-06). La API lo vuelve a comprobar y responde 403 `SinPermiso`.

/** Entregar tarjetas físicas en taquilla: Operador y Administrador. */
export const puedeEntregarTarjetas = (sesion: Sesion | null | undefined) =>
  sesion?.rol === 'Operador' || sesion?.rol === 'Administrador'

/** Apagar la alarma: Seguridad y Administrador; el Operador de taquilla no (RF-06 CA3, RF-34 CA2). */
export const puedeApagarAlarma = (sesion: Sesion | null | undefined) =>
  sesion?.rol === 'Seguridad' || sesion?.rol === 'Administrador'
