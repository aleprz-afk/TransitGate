import * as signalR from '@microsoft/signalr'

// Eventos del hub /hubs/eventos (Contratos de integración, C6). El OpenAPI no describe SignalR,
// así que estos tipos se mantienen a mano: si cambia el contrato, cambian aquí.

export type EstadoTorniquete = 'Cerrado' | 'EsperandoCruce' | 'Alarma' | 'ModoRegistro'
export type ResultadoIntento = 'Exitoso' | 'Rechazado' | 'Fraude'
export type EstadoAlerta = 'Abierta' | 'Atendida' | 'Resuelta'

export interface EstadoTorniqueteEvento {
  torniqueteId: string
  estado: EstadoTorniquete
  estadoDesde: string | null // UTC; null si no ha cambiado desde que arrancó la API
  esp32Conectado: boolean
  ultimaSenal: string | null // UTC; null si no ha llegado ningún latido
  visionConectada: boolean
  desincronizado: boolean
}

export interface IntentoRegistradoEvento {
  intentoId: number
  torniqueteId: string
  resultado: ResultadoIntento
  motivo: string | null
  fecha: string
}

export interface AlertaNuevaEvento {
  alertaId: string
  torniqueteId: string
  tipo: string
  fecha: string
}

export interface AlertaActualizadaEvento {
  alertaId: string
  estado: EstadoAlerta
}

/** Resultado de una lectura en modo registro (RF-10): "Tarjeta entregada" o "Tarjeta ya registrada". */
export interface RegistroTarjetaEvento {
  torniqueteId: string
  solicitudId: string
  resultado: 'Ligado' | 'UidYaRegistrado' | 'SolicitudNoPendiente'
  fecha: string
}

export interface ManejadoresEventos {
  EstadoTorniquete?: (e: EstadoTorniqueteEvento) => void
  IntentoRegistrado?: (e: IntentoRegistradoEvento) => void
  AlertaNueva?: (e: AlertaNuevaEvento) => void
  AlertaActualizada?: (e: AlertaActualizadaEvento) => void
  RegistroTarjeta?: (e: RegistroTarjetaEvento) => void
}

/** Abre la conexión al hub y registra los manejadores. Devuelve la conexión para cerrarla con stop(). */
export function conectarEventos(manejadores: ManejadoresEventos) {
  const conexion = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/eventos')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  for (const [evento, manejador] of Object.entries(manejadores)) {
    if (manejador) conexion.on(evento, manejador)
  }

  return conexion
}
