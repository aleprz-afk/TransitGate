import { useEffect, useState } from 'react'
import { api, type AlertaResumen } from './api/cliente'
import { conectarEventos, type EstadoTorniqueteEvento, type IntentoRegistradoEvento } from './api/eventos'
import { horaColombia } from './hora'

// Panel de prueba del personal: torniquetes, alertas e intentos en vivo (HU-19 a HU-22).
// Es el punto de partida del panel de operación; Ana lo reemplaza por las páginas reales.

type Conexion = 'Conectando' | 'Conectado' | 'Reconectando' | 'Desconectado'

export default function Panel({ nombre, alSalir }: { nombre: string; alSalir: () => void }) {
  const [conexion, setConexion] = useState<Conexion>('Conectando')
  const [torniquetes, setTorniquetes] = useState<Record<string, EstadoTorniqueteEvento>>({})
  const [intentos, setIntentos] = useState<IntentoRegistradoEvento[]>([])
  const [alertas, setAlertas] = useState<AlertaResumen[]>([])

  async function cargarAlertas() {
    const { data } = await api.GET('/api/alertas', { params: { query: { estado: 'Abierta' } } })
    if (data) setAlertas(data)
  }

  useEffect(() => {
    // Al conectarse, el hub manda el estado actual de cada torniquete; después, uno por cambio.
    const hub = conectarEventos({
      EstadoTorniquete: (e) => setTorniquetes((t) => ({ ...t, [e.torniqueteId]: e })),
      IntentoRegistrado: (e) => setIntentos((l) => [e, ...l].slice(0, 20)),
      AlertaNueva: () => void cargarAlertas(),
      AlertaActualizada: () => void cargarAlertas(),
    })
    // En desarrollo, StrictMode monta el efecto dos veces: la primera conexión se cierra durante la
    // negociación (la consola lo muestra como error) y sus avisos ya no deben cambiar el estado.
    let vigente = true
    const marcar = (estado: Conexion) => vigente && setConexion(estado)
    hub.onreconnecting(() => marcar('Reconectando'))
    hub.onreconnected(() => marcar('Conectado'))
    hub.onclose(() => marcar('Desconectado'))
    hub.start().then(
      () => marcar('Conectado'),
      () => marcar('Desconectado'),
    )
    void cargarAlertas()
    return () => {
      vigente = false
      void hub.stop()
    }
  }, [])

  async function apagarAlarma(alertaId: string) {
    await api.POST('/api/alertas/{id}/apagar-alarma', { params: { path: { id: alertaId } } })
  }

  return (
    <main>
      <h1>TransitGate</h1>
      <p className="nota">
        Panel de prueba · {nombre} · tiempo real: {conexion} · <button onClick={alSalir}>Cerrar sesión</button>
      </p>

      <h2>Torniquetes</h2>
      {Object.values(torniquetes).length === 0 && <p className="nota">Sin datos todavía.</p>}
      <ul className="lista">
        {Object.values(torniquetes).map((t) => (
          <li key={t.torniqueteId} className={t.estado === 'Alarma' ? 'alarma' : undefined}>
            <strong>{t.torniqueteId}</strong> · {t.estado} desde {horaColombia(t.estadoDesde)}
            <br />
            ESP32 {t.esp32Conectado ? 'en línea' : `fuera de línea desde ${horaColombia(t.ultimaSenal)}`} ·
            visión {t.visionConectada ? 'en línea' : 'fuera de línea'}
            {t.desincronizado && ' · desincronizado'}
          </li>
        ))}
      </ul>

      <h2>Alertas abiertas</h2>
      {alertas.length === 0 && <p className="nota">Ninguna.</p>}
      <ul className="lista">
        {alertas.map((a) => (
          <li key={a.alertaId} className="alarma">
            {a.torniqueteId} · {horaColombia(a.fecha)}{' '}
            <button onClick={() => void apagarAlarma(a.alertaId)}>Apagar alarma</button>
          </li>
        ))}
      </ul>

      <h2>Intentos recientes</h2>
      {intentos.length === 0 && <p className="nota">Llegan en vivo desde que se abre la página.</p>}
      <ul className="lista">
        {intentos.map((i) => (
          <li key={i.intentoId}>
            {horaColombia(i.fecha)} · {i.torniqueteId} · {i.resultado}
            {i.motivo && ` (${i.motivo})`}
          </li>
        ))}
      </ul>
    </main>
  )
}
