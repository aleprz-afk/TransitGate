import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { api, mensajeDe, type Esquema } from '../../api/cliente'
import type { EventoViajeEvento } from '../../api/eventos'
import { Cargando } from '../../App'
import { enPesos, horaColombia, textoMotivo, textoSoporte } from '../../textos'
import { useEvento } from '../../tiempoReal'

/**
 * RF-16: saldo y estado de la tarjeta, con lo que el cliente puede hacer en cada estado: pedir la física
 * (RF-08) o confirmar que la recibió (RF-11). El saldo cambia en vivo con cada paso o recarga (CA2, CA3).
 */
export default function MiTarjeta() {
  const [tarjeta, setTarjeta] = useState<Esquema['MiTarjetaJson']>()
  const [ultimoViaje, setUltimoViaje] = useState<EventoViajeEvento>()
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'error'; texto: string }>()
  const [ocupado, setOcupado] = useState(false)

  async function cargar() {
    const { data } = await api.GET('/api/tarjetas/mi-tarjeta')
    if (data) setTarjeta(data)
  }

  useEffect(() => {
    void cargar()
  }, [])

  useEvento('SaldoActualizado', (e) => setTarjeta((t) => (t ? { ...t, saldo: e.saldo } : t)))
  useEvento('EventoViaje', setUltimoViaje)

  async function pedirFisica() {
    setOcupado(true)
    const { data, error } = await api.POST('/api/tarjetas/mi-tarjeta/solicitud-fisica')
    setOcupado(false)
    setAviso(data
      ? { tipo: 'ok', texto: 'Listo. Recoge tu tarjeta en la taquilla con tu documento.' }
      : { tipo: 'error', texto: mensajeDe(error) })
    await cargar()
  }

  async function confirmarRecepcion() {
    setOcupado(true)
    const { data, error } = await api.POST('/api/tarjetas/mi-tarjeta/confirmar-recepcion')
    setOcupado(false)
    setAviso(data
      ? { tipo: 'ok', texto: 'Tu tarjeta quedó activa. Ya puedes pasar por el torniquete.' }
      : { tipo: 'error', texto: mensajeDe(error) })
    await cargar()
  }

  if (!tarjeta) return <Cargando />
  const estado = tarjeta.soporte?.estado

  return (
    <>
      <section className="tarjeta saldo">
        <span className="nota">Saldo</span>
        <strong className="cifra">{enPesos(tarjeta.saldo)}</strong>
        <Link to="/recargar" className="boton principal">Recargar</Link>
      </section>

      <section className="tarjeta">
        <h2>Tu tarjeta</h2>
        <p>{textoSoporte(tarjeta.soporte)}</p>
        {(!tarjeta.soporte || estado === 'Bloqueada') && (
          <button className="principal" onClick={() => void pedirFisica()} disabled={ocupado}>
            Pedir tarjeta física
          </button>
        )}
        {estado === 'Entregada' && (
          <button className="principal" onClick={() => void confirmarRecepcion()} disabled={ocupado}>
            Ya la recibí
          </button>
        )}
        {aviso && <p className={aviso.tipo === 'ok' ? 'exito' : 'error'} role="status">{aviso.texto}</p>}
      </section>

      {ultimoViaje && (
        <section className={`tarjeta ${ultimoViaje.resultado === 'Exitoso' ? 'exito-borde' : 'alarma'}`} role="status">
          <strong>{ultimoViaje.resultado === 'Exitoso' ? 'Pasaste' : 'No pasaste'}</strong> · {ultimoViaje.torniquete} ·{' '}
          {horaColombia(ultimoViaje.fecha)}
          <br />
          {ultimoViaje.resultado === 'Exitoso'
            ? `Se cobraron ${enPesos(ultimoViaje.tarifa ?? 0)}.`
            : `${textoMotivo(ultimoViaje.motivo)}.`}
        </section>
      )}
    </>
  )
}
