// La API manda las horas en UTC; se muestran en hora de Colombia (RNF-42).
const formato = new Intl.DateTimeFormat('es-CO', {
  timeZone: 'America/Bogota',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
})

export function horaColombia(utc: string | null | undefined) {
  return utc ? formato.format(new Date(utc)) : '—'
}
