import * as signalR from '@microsoft/signalr'
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { nombresEventos, type EstadoTorniqueteEvento, type Eventos } from './api/eventos'

// Una sola conexión al hub mientras hay sesión (el hub pide la cookie). La API pone al personal en el grupo
// "operacion" y a cada cliente en el suyo; aquí solo se reparte cada evento a las páginas que lo escuchan.

export type EstadoConexion = 'Conectando' | 'Conectado' | 'Reconectando' | 'Desconectado'

type Manejador = (contenido: never) => void

interface ContextoTiempoReal {
  estado: EstadoConexion
  suscribir: (evento: keyof Eventos, manejador: Manejador) => () => void
  /** El último EstadoTorniquete de cada torniquete recibido por esta conexión. */
  estadosTorniquetes: () => Record<string, EstadoTorniqueteEvento>
}

const Contexto = createContext<ContextoTiempoReal>({ estado: 'Desconectado', suscribir: () => () => {}, estadosTorniquetes: () => ({}) })

export function TiempoReal({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<EstadoConexion>('Conectando')
  const manejadores = useRef(new Map<keyof Eventos, Set<Manejador>>())
  // El hub manda el estado de cada torniquete solo al conectarse y cuando cambia: se guarda el último para
  // que una página que se monta después (volver de Taquilla al Panel) no se quede esperando el próximo cambio.
  const estados = useRef<Record<string, EstadoTorniqueteEvento>>({})

  useEffect(() => {
    const conexion = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/eventos')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conexion.on('EstadoTorniquete', (e: EstadoTorniqueteEvento) => {
      estados.current = { ...estados.current, [e.torniqueteId]: e }
    })
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

  const estadosTorniquetes = useCallback(() => estados.current, [])

  return <Contexto.Provider value={{ estado, suscribir, estadosTorniquetes }}>{children}</Contexto.Provider>
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

/** El último estado conocido de cada torniquete, para empezar con él al montar la página. */
export function useEstadosTorniquetes() {
  return useContext(Contexto).estadosTorniquetes
}

export function useEstadoConexion() {
  return useContext(Contexto).estado
}
