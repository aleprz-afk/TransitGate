import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api, mensajeDe, type Esquema, type TorniqueteResumen } from '../../api/cliente'
import { fechaColombia } from '../../textos'
import { useEvento } from '../../tiempoReal'

type Solicitud = Esquema['SolicitudResumen']

interface Entrega {
  solicitud: Solicitud
  torniqueteId: string
  hasta: number
  aviso?: { tipo: 'esperando' | 'ok' | 'error'; texto: string }
  terminada: boolean
}

/**
 * Entrega de la tarjeta física (RF-36, RF-09, RF-10): lista de solicitudes pendientes, búsqueda por documento y
 * el botón "Entregar", que pone el lector en modo registro. El resultado de la lectura llega por el hub.
 */
export default function Taquilla() {
  const [solicitudes, setSolicitudes] = useState<Solicitud[]>()
  const [documento, setDocumento] = useState('')
  const [buscado, setBuscado] = useState<string>()
  const [torniquetes, setTorniquetes] = useState<TorniqueteResumen[]>([])
  const [torniqueteId, setTorniqueteId] = useState<string>()
  const [entrega, setEntrega] = useState<Entrega>()
  const [error, setError] = useState<string>()
  const [ahora, setAhora] = useState(() => Date.now())
  const entregaActual = useRef(entrega)
  useEffect(() => {
    entregaActual.current = entrega
  })

  async function cargar(porDocumento = buscado) {
    const { data } = await api.GET('/api/tarjetas/solicitudes', {
      params: { query: porDocumento ? { documento: porDocumento } : {} },
    })
    if (data) setSolicitudes(data)
  }

  useEffect(() => {
    void cargar()
    api.GET('/api/torniquetes').then(({ data }) => {
      if (!data) return
      setTorniquetes(data)
      setTorniqueteId((actual) => actual ?? data[0]?.id)
    })
    // Solo la carga inicial: después la lista se recarga con cada evento.
  }, [])

  // La cuenta regresiva del modo registro (S6: 60 s).
  useEffect(() => {
    if (!entrega || entrega.terminada) return
    const reloj = setInterval(() => setAhora(Date.now()), 500)
    return () => clearInterval(reloj)
  }, [entrega])

  useEvento('SolicitudNueva', () => void cargar())

  useEvento('RegistroTarjeta', (e) => {
    const actual = entregaActual.current
    if (!actual || actual.terminada || e.solicitudId !== actual.solicitud.solicitudId) return

    if (e.resultado === 'Ligado') {
      setEntrega({ ...actual, terminada: true, aviso: { tipo: 'ok', texto: `Tarjeta entregada a ${actual.solicitud.nombreCompleto}. Pídele que confirme la recepción en el portal.` } })
      void cargar()
    } else if (e.resultado === 'UidYaRegistrado') {
      // RF-10 CA2: el torniquete sigue en modo registro para probar con otra tarjeta.
      setEntrega({ ...actual, aviso: { tipo: 'error', texto: 'Tarjeta ya registrada. Acerque otra tarjeta nueva.' } })
    } else {
      setEntrega({ ...actual, terminada: true, aviso: { tipo: 'error', texto: 'Esa solicitud ya se había entregado.' } })
      void cargar()
    }
  })

  useEvento('EstadoTorniquete', (e) => {
    const actual = entregaActual.current
    if (!actual || actual.terminada || e.torniqueteId !== actual.torniqueteId || e.estado === 'ModoRegistro') return

    // Salió del modo registro sin ligar (RF-09 CA3 por tiempo, o RF-28 CA5 por un cruce). Se espera un
    // momento por si el RegistroTarjeta viene en camino.
    setTimeout(() => {
      const despues = entregaActual.current
      if (!despues || despues.terminada || despues.solicitud.solicitudId !== actual.solicitud.solicitudId) return
      setEntrega({
        ...despues,
        terminada: true,
        aviso: {
          tipo: 'error',
          texto: e.estado === 'Alarma'
            ? 'Se canceló el modo registro: alguien cruzó el torniquete. La solicitud sigue pendiente.'
            : 'Se acabó el tiempo sin leer una tarjeta. La solicitud sigue pendiente.',
        },
      })
    }, 1500)
  })

  async function buscar(evento: FormEvent) {
    evento.preventDefault()
    const valor = documento.trim() || undefined
    setBuscado(valor)
    await cargar(valor)
  }

  async function entregar(solicitud: Solicitud) {
    if (!torniqueteId) return
    setError(undefined)
    const { data, error } = await api.POST('/api/torniquetes/{id}/modo-registro', {
      params: { path: { id: torniqueteId } },
      body: { solicitudId: solicitud.solicitudId },
    })
    if (!data) {
      setError(mensajeDe(error))
      return
    }

    setAhora(Date.now())
    setEntrega({
      solicitud,
      torniqueteId,
      hasta: Date.now() + data.expiraEn * 1000,
      terminada: false,
      aviso: { tipo: 'esperando', texto: 'Acerque la tarjeta nueva al lector.' },   // RF-09 CA1
    })
  }

  const segundos = entrega ? Math.max(0, Math.ceil((entrega.hasta - ahora) / 1000)) : 0
  const nombreTorniquete = (id: string) => torniquetes.find((t) => t.id === id)?.nombre ?? id

  return (
    <>
      <h1>Taquilla</h1>

      {entrega && (
        <section className={`tarjeta entrega ${entrega.aviso?.tipo === 'ok' ? 'exito-borde' : entrega.aviso?.tipo === 'error' ? 'alarma' : 'activo-borde'}`} role="status">
          <div className="fila-dato">
            <strong>{entrega.solicitud.nombreCompleto}</strong>
            {!entrega.terminada && <span className="cifra-chica">{segundos} s</span>}
          </div>
          {!entrega.terminada && <p className="nota">{nombreTorniquete(entrega.torniqueteId)} en modo registro</p>}
          <p>{entrega.aviso?.texto}</p>
          {entrega.terminada && <button onClick={() => setEntrega(undefined)}>Listo</button>}
        </section>
      )}

      <form className="busqueda" onSubmit={buscar}>
        <input
          inputMode="numeric"
          placeholder="Buscar por documento"
          value={documento}
          onChange={(e) => setDocumento(e.target.value)}
          aria-label="Documento del cliente"
        />
        <button type="submit">Buscar</button>
        {buscado && (
          <button type="button" className="enlace" onClick={() => { setDocumento(''); setBuscado(undefined); void cargar(undefined) }}>
            Ver todas
          </button>
        )}
      </form>

      {torniquetes.length > 1 && (
        <label className="campo-linea">
          Lector
          <select value={torniqueteId} onChange={(e) => setTorniqueteId(e.target.value)}>
            {torniquetes.map((t) => (
              <option key={t.id} value={t.id}>{t.nombre}</option>
            ))}
          </select>
        </label>
      )}

      {error && <p className="error" role="alert">{error}</p>}

      <h2>Solicitudes pendientes</h2>
      {solicitudes?.length === 0 && (
        <p className="nota">{buscado ? 'Sin solicitud pendiente para ese documento.' : 'No hay solicitudes pendientes.'}</p>
      )}
      <ul className="lista">
        {solicitudes?.map((s) => (
          <li key={s.solicitudId}>
            <div className="fila-dato">
              <div>
                <strong>{s.nombreCompleto}</strong>
                <div className="nota">Documento {s.documento} · {fechaColombia(s.fecha)}</div>
              </div>
              <button className="principal" onClick={() => void entregar(s)} disabled={!!entrega && !entrega.terminada}>
                Entregar
              </button>
            </div>
          </li>
        ))}
      </ul>
    </>
  )
}
