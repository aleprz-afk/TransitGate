import { useEffect, useState } from 'react'
import { api, type AlertaResumen, type IntentoResumen } from '../../api/cliente'
import type { EstadoTorniqueteEvento } from '../../api/eventos'
import { horaColombia, textoEstadoTorniquete, textoMotivo } from '../../textos'
import { useEvento } from '../../tiempoReal'

interface Intento extends IntentoResumen {
  torniqueteId: string
}

const MAXIMO_INTENTOS = 30

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
  useEvento('AlertaNueva', () => void cargarAlertas())
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
          <div>
            <strong>Colado en {nombres[a.torniqueteId] ?? a.torniqueteId}</strong>
            <br />
            <span>Desde las {horaColombia(a.fecha)}</span>
          </div>
          <button className="peligro" onClick={() => void apagarAlarma(a.alertaId)} disabled={apagando === a.alertaId}>
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
          <li key={i.intentoId} className={i.resultado === 'Fraude' ? 'alarma' : i.resultado === 'Rechazado' ? 'rechazado' : undefined}>
            <div className="fila-dato">
              <strong>{i.resultado === 'Exitoso' ? 'Paso' : i.resultado === 'Fraude' ? 'Colado' : 'Rechazado'}</strong>
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
