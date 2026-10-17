// Textos para mostrar lo que la API manda como códigos (RNF-32: mensajes en español claro).

// La API manda las horas en UTC; se muestran en hora de Colombia (RNF-42).
const hora = new Intl.DateTimeFormat('es-CO', { timeZone: 'America/Bogota', hour: '2-digit', minute: '2-digit', second: '2-digit' })
const fechaHora = new Intl.DateTimeFormat('es-CO', {
  timeZone: 'America/Bogota',
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})
const pesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })

export const horaColombia = (utc: string | null | undefined) => (utc ? hora.format(new Date(utc)) : '—')
export const fechaColombia = (utc: string | null | undefined) => (utc ? fechaHora.format(new Date(utc)) : '—')
export const enPesos = (valor: number) => pesos.format(valor)

/** Motivos de un intento rechazado (RF v2, sección 1) y de una lectura en modo registro. */
const motivos: Record<string, string> = {
  TarjetaNoRegistrada: 'Tarjeta no registrada',
  SoporteNoActivo: 'La tarjeta todavía no está activa',
  SoporteBloqueado: 'Tarjeta bloqueada',
  SaldoInsuficiente: 'Saldo insuficiente',
  FirmaInvalida: 'Celular no reconocido',
  DesafioInvalido: 'Celular no reconocido',
  TorniqueteOcupado: 'Torniquete ocupado',
  TorniqueteEnAlarma: 'Torniquete en alarma',
  FraudeSinValidacion: 'Paso sin validar',
  UidYaRegistrado: 'Tarjeta ya registrada',
  SolicitudNoPendiente: 'La solicitud ya se entregó',
}
export const textoMotivo = (motivo: string | null | undefined) => (motivo ? (motivos[motivo] ?? motivo) : '')

const estadosTorniquete: Record<string, string> = {
  Cerrado: 'Cerrado',
  EsperandoCruce: 'Esperando cruce',
  Alarma: 'En alarma',
  ModoRegistro: 'Modo registro',
}
export const textoEstadoTorniquete = (estado: string) => estadosTorniquete[estado] ?? estado

/** RF-16 CA1: el tipo y el estado del soporte, con lo que el cliente tiene que hacer. */
export function textoSoporte(soporte: { tipo: string; estado: string } | null | undefined): string {
  if (!soporte) return 'Todavía no tienes tarjeta física. Pídela y recógela en la taquilla.'
  const tipo = soporte.tipo === 'fisico' ? 'Tarjeta física' : 'Celular'
  switch (soporte.estado) {
    case 'Solicitada':
      return `${tipo} solicitada: recógela en la taquilla con tu documento.`
    case 'Entregada':
      return `${tipo} entregada: confirma que la recibiste para empezar a usarla.`
    case 'Activa':
      return `${tipo} activa.`
    case 'Bloqueada':
      return `${tipo} bloqueada. Puedes pedir una nueva; tu saldo se conserva.`
    default:
      return `${tipo}: ${soporte.estado}`
  }
}
