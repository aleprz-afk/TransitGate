// Eventos del hub /hubs/eventos (Contratos de integración, C6). El OpenAPI no describe SignalR,
// así que estos tipos se mantienen a mano: si cambia el contrato, cambian aquí.
// La conexión la abre TiempoReal (src/tiempoReal.tsx) cuando hay sesión; las páginas se suscriben con useEvento.

export type EstadoTorniquete = 'Cerrado' | 'EsperandoCruce' | 'Alarma' | 'ModoRegistro'
export type ResultadoIntento = 'Exitoso' | 'Rechazado' | 'Fraude'
export type EstadoAlerta = 'Abierta' | 'Atendida' | 'Resuelta'

// Grupo "operacion": solo el personal.

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

/** Una solicitud de tarjeta física nueva para la taquilla (RF-36). `cliente` va abreviado: "Luisa R.". */
export interface SolicitudNuevaEvento {
  solicitudId: string
  cliente: string
  fecha: string
}

// Grupo del cliente: solo los recibe el dueño de la tarjeta.

export interface SaldoActualizadoEvento {
  saldo: number
  causa: 'Paso' | 'Recarga' | 'Reversion'
  fecha: string
}

/** Un intento con la tarjeta del cliente (RF-39 CA4). `tarifa` y `motivo` son null según el resultado. */
export interface EventoViajeEvento {
  intentoId: number
  resultado: 'Exitoso' | 'Rechazado'
  motivo: string | null
  torniquete: string
  tarifa: number | null
  fecha: string
}

/** Cada evento del hub con el tipo de su contenido. */
export interface Eventos {
  EstadoTorniquete: EstadoTorniqueteEvento
  IntentoRegistrado: IntentoRegistradoEvento
  AlertaNueva: AlertaNuevaEvento
  AlertaActualizada: AlertaActualizadaEvento
  RegistroTarjeta: RegistroTarjetaEvento
  SolicitudNueva: SolicitudNuevaEvento
  SaldoActualizado: SaldoActualizadoEvento
  EventoViaje: EventoViajeEvento
}

export const nombresEventos: (keyof Eventos)[] = [
  'EstadoTorniquete',
  'IntentoRegistrado',
  'AlertaNueva',
  'AlertaActualizada',
  'RegistroTarjeta',
  'SolicitudNueva',
  'SaldoActualizado',
  'EventoViaje',
]
