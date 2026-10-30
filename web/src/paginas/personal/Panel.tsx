import { useEffect, useState } from 'react'
import { BellOff, CircleCheck, CircleX, ShieldAlert, TriangleAlert } from 'lucide-react'
import { api, type AlertaResumen, type IntentoResumen } from '../../api/cliente'
import type { EstadoTorniqueteEvento } from '../../api/eventos'
import { vibrarAlarma } from '../../movimiento'
import { horaColombia, textoEstadoTorniquete, textoMotivo } from '../../textos'
import { useEvento } from '../../tiempoReal'

interface Intento extends IntentoResumen {
  torniqueteId: string
}

const MAXIMO_INTENTOS = 30

// Ventana de un paso (S4): Operacion:ExpiracionPasoSegundos en appsettings.json de la API. El evento no la
// trae, así que si se cambia allá hay que cambiarla aquí.
const VENTANA_PASO_MS = 10_000

/** Dibujo del torniquete: el brazo gira cuando hay un paso abierto (EsperandoCruce). */
function DibujoTorniquete({ abierto }: { abierto: boolean }) {
  return (
    <svg className={`dibujo-torniquete${abierto ? ' abierto' : ''}`} viewBox="0 0 120 90" aria-hidden="true">
      <rect x="34" y="30" width="22" height="58" rx="4" className="poste" />
      <rect className="brazo" x="45" y="55" width="56" height="6" rx="3" />
      <circle cx="45" cy="58" r="6" className="eje" />
    </svg>
  )
}

/**
 * Barra que se vacía durante la ventana del paso, desde la hora en que se abrió. Se monta de nuevo con cada
 * paso (key = estadoDesde); la hora se toma una vez al montar y la animación CSS hace el resto.
 */
function BarraVentana({ desde }: { desde: string }) {
  const [montada] = useState(() => Date.now())
  const transcurrido = Math.max(0, montada - new Date(desde).getTime())
  if (transcurrido >= VENTANA_PASO_MS) return null
  return (
    <div className="barra-ventana" role="img" aria-label={`Ventana del paso: ${VENTANA_PASO_MS / 1000} s para cruzar`}>
      <div style={{ animationDuration: `${VENTANA_PASO_MS}ms`, animationDelay: `-${transcurrido}ms` }} />
    </div>
  )
}

/**
 * Panel de operación (HU-19 a HU-22): estado y conexión de cada torniquete (RF-31, RF-37), alertas de colado
 * con el botón para apagar la alarma (RF-33, RF-34) e intentos en vivo (RF-32). Todo llega por el hub.
 */
export default function Panel() {
  const [nombres, setNombres] = useState<Record<string, string>>({})
  const [estados, setEstados] = useState<Record<string, EstadoTorniqueteEvento>>({})
  const [alertas, setAlertas] = useState<AlertaResumen[]>([])
  const [intentos, setIntentos] = useState<Intento[]>([])
  const [apagando, setApagando] = useState<string>()

  async function cargarAlertas() {
    const { data } = await api.GET('/api/alertas', { params: { query: { estado: 'Abierta' } } })
    if (data) setAlertas(data)
  }

  useEffect(() => {
    // Carga inicial; después todo llega en vivo. El estado de cada torniquete lo manda el hub al conectarse.
    void (async () => {
      const { data: torniquetes } = await api.GET('/api/torniquetes')
      if (!torniquetes) return
      setNombres(Object.fromEntries(torniquetes.map((t) => [t.id, t.nombre])))
      const historiales = await Promise.all(
        torniquetes.map(async (t) => {
          const { data } = await api.GET('/api/torniquetes/{id}/intentos', { params: { path: { id: t.id } } })
          return (data ?? []).map((i) => ({ ...i, torniqueteId: t.id }))
        }),
      )
      setIntentos(historiales.flat().sort((a, b) => b.fecha.localeCompare(a.fecha)).slice(0, MAXIMO_INTENTOS))
    })()
    void cargarAlertas()
  }, [])

  useEvento('EstadoTorniquete', (e) => setEstados((actual) => ({ ...actual, [e.torniqueteId]: e })))
  useEvento('IntentoRegistrado', (e) => setIntentos((lista) => [e, ...lista.filter((i) => i.intentoId !== e.intentoId)].slice(0, MAXIMO_INTENTOS)))
  useEvento('AlertaNueva', () => {
    vibrarAlarma()
    void cargarAlertas()
  })
  useEvento('AlertaActualizada', () => void cargarAlertas())

  async function apagarAlarma(alertaId: string) {
    setApagando(alertaId)
    await api.POST('/api/alertas/{id}/apagar-alarma', { params: { path: { id: alertaId } } })
    setApagando(undefined)
    await cargarAlertas()
  }

  const torniquetes = Object.values(estados).sort((a, b) => a.torniqueteId.localeCompare(b.torniqueteId))

  return (
    <>
      {alertas.map((a) => (
        <section key={a.alertaId} className="tarjeta alarma alerta-colado" role="alert">
          <ShieldAlert className="icono-grande" aria-hidden="true" />
          <div className="alerta-texto">
            <strong>Colado en {nombres[a.torniqueteId] ?? a.torniqueteId}</strong>
            <br />
            <span>Desde las {horaColombia(a.fecha)}</span>
          </div>
          <button className="peligro con-icono" onClick={() => void apagarAlarma(a.alertaId)} disabled={apagando === a.alertaId}>
            <BellOff aria-hidden="true" />
            {apagando === a.alertaId ? 'Apagando…' : 'Apagar alarma'}
          </button>
        </section>
      ))}

      <h2>Torniquetes</h2>
      {torniquetes.length === 0 && <p className="nota">Esperando el estado de los torniquetes…</p>}
      <div className="rejilla">
        {torniquetes.map((t) => (
          <section key={t.torniqueteId} className={`tarjeta torniquete ${t.estado === 'Alarma' ? 'alarma' : ''}`}>
            <div className="fila-dato">
              <strong>{nombres[t.torniqueteId] ?? t.torniqueteId}</strong>
              <span className={`etiqueta ${t.estado === 'Alarma' ? 'peligro' : t.estado === 'Cerrado' ? '' : 'activo'}`}>
                {textoEstadoTorniquete(t.estado)}
              </span>
            </div>
            <DibujoTorniquete abierto={t.estado === 'EsperandoCruce'} />
            {t.estado === 'EsperandoCruce' && t.estadoDesde && <BarraVentana key={t.estadoDesde} desde={t.estadoDesde} />}
            {t.estadoDesde && <p className="nota">Desde las {horaColombia(t.estadoDesde)}</p>}
            <ul className="conexiones">
              <li className={t.esp32Conectado ? 'en-linea' : 'fuera'}>
                ESP32 {t.esp32Conectado ? 'en línea' : `fuera de línea${t.ultimaSenal ? ` desde las ${horaColombia(t.ultimaSenal)}` : ''}`}
              </li>
              <li className={t.visionConectada ? 'en-linea' : 'fuera'}>Visión {t.visionConectada ? 'en línea' : 'fuera de línea'}</li>
              {t.desincronizado && <li className="fuera">Desincronizado: el brazo no hace lo que se le pide</li>}
            </ul>
          </section>
        ))}
      </div>

      <h2>Intentos recientes</h2>
      {intentos.length === 0 && <p className="nota">Todavía no hay intentos.</p>}
      <ul className="lista">
        {intentos.map((i) => (
          <li key={i.intentoId} className={`entra ${i.resultado === 'Fraude' ? 'alarma' : i.resultado === 'Rechazado' ? 'rechazado' : ''}`}>
            <div className="fila-dato">
              <strong className="con-icono">
                {i.resultado === 'Exitoso' ? <CircleCheck className="icono-exito" aria-hidden="true" />
                  : i.resultado === 'Fraude' ? <TriangleAlert className="icono-alarma" aria-hidden="true" />
                  : <CircleX className="icono-activo" aria-hidden="true" />}
                {i.resultado === 'Exitoso' ? 'Paso' : i.resultado === 'Fraude' ? 'Colado' : 'Rechazado'}
              </strong>
              <span className="nota">{horaColombia(i.fecha)}</span>
            </div>
            <div className="fila-dato nota">
              <span>{nombres[i.torniqueteId] ?? i.torniqueteId}</span>
              <span>{textoMotivo(i.motivo)}</span>
            </div>
          </li>
        ))}
      </ul>
    </>
  )
}
