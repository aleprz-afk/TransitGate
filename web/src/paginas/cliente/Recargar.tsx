import { useState, type FormEvent } from 'react'
import { api, mensajeDe, type Esquema } from '../../api/cliente'
import { enPesos } from '../../textos'

const MONTO_MINIMO = 1000
const MONTO_MAXIMO = 200000
const SUGERIDOS = [5000, 10000, 20000, 50000]

// crypto.randomUUID solo existe en HTTPS o localhost, y en la demo el celular entra por http://<IP>:5000.
const nuevaClave = () => Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, '0')).join('')

/**
 * RF-15: recarga con pago simulado. Cada intento lleva su Idempotency-Key, así un doble clic o un reintento
 * no cobra dos veces (CA5); se genera una nueva cuando la API ya respondió.
 */
export default function Recargar() {
  const [monto, setMonto] = useState('')
  const [simularRechazo, setSimularRechazo] = useState(false)
  const [clave, setClave] = useState(nuevaClave)
  const [enviando, setEnviando] = useState(false)
  const [resultado, setResultado] = useState<Esquema['RecargaJson']>()
  const [error, setError] = useState<string>()

  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    setError(undefined)
    setResultado(undefined)

    const valor = Number(monto)
    if (!Number.isInteger(valor) || valor < MONTO_MINIMO || valor > MONTO_MAXIMO) {
      setError(`El monto debe estar entre ${enPesos(MONTO_MINIMO)} y ${enPesos(MONTO_MAXIMO)}.`)   // S8, CA3
      return
    }

    setEnviando(true)
    try {
      const { data, error } = await api.POST('/api/tarjetas/mi-tarjeta/recargas', {
        params: { header: { 'Idempotency-Key': clave } },
        body: { monto: valor, simularRechazo },
      })
      if (data) setResultado(data)
      else setError(mensajeDe(error, 'No se pudo hacer la recarga. Intenta de nuevo.'))
      // La API respondió: el intento terminó y el siguiente lleva otra clave.
      setClave(nuevaClave())
    } catch {
      // Sin respuesta (red caída): se reintenta con la misma clave, así la API no cobra dos veces.
      setError('Sin conexión con el servidor. Intenta de nuevo: no se cobrará dos veces.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <>
      <h1>Recargar</h1>
      <form className="formulario tarjeta" onSubmit={enviar} noValidate>
        <label>
          Monto en pesos
          <input
            inputMode="numeric"
            value={monto}
            onChange={(e) => setMonto(e.target.value.replace(/\D/g, ''))}
            placeholder="10000"
            aria-invalid={!!error}
          />
          <span className="nota">Entre {enPesos(MONTO_MINIMO)} y {enPesos(MONTO_MAXIMO)}.</span>
        </label>
        <div className="sugeridos">
          {SUGERIDOS.map((valor) => (
            <button type="button" key={valor} onClick={() => setMonto(String(valor))} aria-pressed={monto === String(valor)}>
              {enPesos(valor)}
            </button>
          ))}
        </div>
        <label className="casilla">
          <input type="checkbox" checked={simularRechazo} onChange={(e) => setSimularRechazo(e.target.checked)} />
          <span>Simular un pago rechazado (para la demo, RF-15 CA2)</span>
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" className="principal" disabled={enviando}>
          {enviando ? 'Procesando pago…' : 'Pagar y recargar'}
        </button>
      </form>

      {resultado && (
        <section className={`tarjeta ${resultado.aprobada ? 'exito-borde' : 'alarma'}`} role="status">
          {resultado.aprobada ? (
            <>
              <strong>Recarga aprobada.</strong> Tu saldo es {enPesos(resultado.saldoNuevo)}.
            </>
          ) : (
            <>
              <strong>El pago fue rechazado.</strong> Tu saldo no cambió: {enPesos(resultado.saldoNuevo)}.
            </>
          )}
        </section>
      )}
    </>
  )
}
