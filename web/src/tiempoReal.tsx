import * as signalR from '@microsoft/signalr'
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { nombresEventos, type Eventos } from './api/eventos'

// Una sola conexión al hub mientras hay sesión (el hub pide la cookie). La API pone al personal en el grupo
// "operacion" y a cada cliente en el suyo; aquí solo se reparte cada evento a las páginas que lo escuchan.

export type EstadoConexion = 'Conectando' | 'Conectado' | 'Reconectando' | 'Desconectado'

type Manejador = (contenido: never) => void

interface ContextoTiempoReal {
  estado: EstadoConexion
  suscribir: (evento: keyof Eventos, manejador: Manejador) => () => void
}

const Contexto = createContext<ContextoTiempoReal>({ estado: 'Desconectado', suscribir: () => () => {} })

export function TiempoReal({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<EstadoConexion>('Conectando')
  const manejadores = useRef(new Map<keyof Eventos, Set<Manejador>>())

  useEffect(() => {
    const conexion = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/eventos')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    for (const nombre of nombresEventos) {
      conexion.on(nombre, (contenido: never) => {
        manejadores.current.get(nombre)?.forEach((manejador) => manejador(contenido))
      })
    }

    // En desarrollo, StrictMode monta el efecto dos veces: la primera conexión se cierra durante la
    // negociación (la consola lo muestra como error) y sus avisos ya no deben cambiar el estado.
    let vigente = true
    const marcar = (nuevo: EstadoConexion) => {
      if (vigente) setEstado(nuevo)
    }
    conexion.onreconnecting(() => marcar('Reconectando'))
    conexion.onreconnected(() => marcar('Conectado'))
    conexion.onclose(() => marcar('Desconectado'))
    conexion.start().then(
      () => marcar('Conectado'),
      () => marcar('Desconectado'),
    )

    return () => {
      vigente = false
      void conexion.stop()
    }
  }, [])

  const suscribir = useCallback((evento: keyof Eventos, manejador: Manejador) => {
    const lista = manejadores.current.get(evento) ?? new Set()
    lista.add(manejador)
    manejadores.current.set(evento, lista)
    return () => void lista.delete(manejador)
  }, [])

  return <Contexto.Provider value={{ estado, suscribir }}>{children}</Contexto.Provider>
}

/** Ejecuta `manejador` con cada evento `evento` del hub mientras el componente esté montado. */
export function useEvento<E extends keyof Eventos>(evento: E, manejador: (contenido: Eventos[E]) => void) {
  const { suscribir } = useContext(Contexto)
  const ultimo = useRef(manejador)
  useEffect(() => {
    ultimo.current = manejador
  })
  useEffect(() => suscribir(evento, ((contenido: Eventos[E]) => ultimo.current(contenido)) as Manejador), [evento, suscribir])
}

export function useEstadoConexion() {
  return useContext(Contexto).estado
}
