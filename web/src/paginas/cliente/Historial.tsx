import { useEffect, useState } from 'react'
import { api, type Esquema } from '../../api/cliente'
import { Cargando } from '../../App'
import { enPesos, fechaColombia, textoMotivo } from '../../textos'
import { useEvento } from '../../tiempoReal'

/**
 * RF-39: los viajes, exitosos y rechazados con el motivo en lenguaje claro (CA2), el más reciente primero.
 * Un paso nuevo aparece sin recargar la página (CA4).
 */
export function Viajes() {
  const [viajes, setViajes] = useState<Esquema['ViajeJson'][]>()

  async function cargar() {
    const { data } = await api.GET('/api/tarjetas/mi-tarjeta/viajes')
    if (data) setViajes(data)
  }

  useEffect(() => {
    void cargar()
  }, [])
  useEvento('EventoViaje', () => void cargar())

  if (!viajes) return <Cargando />

  return (
    <>
      <h1>Tus viajes</h1>
      {viajes.length === 0 && <p className="nota">Todavía no tienes viajes. Aparecerán aquí cada vez que pases por un torniquete.</p>}
      <ul className="lista">
        {viajes.map((v) => (
          <li key={v.intentoId} className={v.resultado === 'Exitoso' ? undefined : 'rechazado'}>
            <div className="fila-dato">
              <strong>{v.torniqueteNombre}</strong>
              <span>{v.resultado === 'Exitoso' ? `−${enPesos(v.tarifa ?? 0)}` : 'Rechazado'}</span>
            </div>
            <div className="fila-dato nota">
              <span>{fechaColombia(v.fecha)}</span>
              <span>{v.resultado === 'Exitoso' ? `Saldo ${enPesos(v.saldoRestante ?? 0)}` : textoMotivo(v.motivo)}</span>
            </div>
          </li>
        ))}
      </ul>
    </>
  )
}

/** RF-17: las recargas, aprobadas y rechazadas, con el saldo resultante, la más reciente primero. */
export function Recargas() {
  const [recargas, setRecargas] = useState<Esquema['RecargaJson'][]>()

  useEffect(() => {
    api.GET('/api/tarjetas/mi-tarjeta/recargas').then(({ data }) => data && setRecargas(data))
  }, [])

  if (!recargas) return <Cargando />

  return (
    <>
      <h1>Tus recargas</h1>
      {recargas.length === 0 && <p className="nota">Todavía no has recargado.</p>}
      <ul className="lista">
        {recargas.map((r) => (
          <li key={r.recargaId} className={r.aprobada ? undefined : 'rechazado'}>
            <div className="fila-dato">
              <strong>{r.aprobada ? `+${enPesos(r.monto)}` : enPesos(r.monto)}</strong>
              <span>{r.aprobada ? 'Aprobada' : 'Rechazada'}</span>
            </div>
            <div className="fila-dato nota">
              <span>{fechaColombia(r.fecha)}</span>
              <span>Saldo {enPesos(r.saldoNuevo)}</span>
            </div>
          </li>
        ))}
      </ul>
    </>
  )
}
