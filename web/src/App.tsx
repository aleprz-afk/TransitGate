import { useEffect, useState, type FormEvent } from 'react'
import { api } from './api/cliente'
import type { components } from './api/esquema'
import Panel from './Panel'

// Página de prueba: con sesión del personal muestra el panel; con sesión de cliente, su saldo.
// Sirve para probar la sesión (cookie de Identity) y los permisos; Ana la reemplaza por las páginas reales.

type Sesion = components['schemas']['RespuestaSesion']
type MiTarjeta = components['schemas']['MiTarjetaJson']

export default function App() {
  // undefined: todavía no se sabe; null: sin sesión.
  const [sesion, setSesion] = useState<Sesion | null | undefined>(undefined)

  useEffect(() => {
    // Al recargar la página, la cookie sigue: se pregunta quién es (401 si no hay sesión).
    api.GET('/api/auth/sesion').then(({ data }) => setSesion(data ?? null))
  }, [])

  async function salir() {
    await api.POST('/api/auth/logout')
    setSesion(null)
  }

  if (sesion === undefined) return <main className="nota">Cargando…</main>
  if (sesion === null) return <Login alEntrar={setSesion} />
  if (sesion.rol === 'Cliente') return <Cliente nombre={sesion.nombre} alSalir={salir} />
  return <Panel nombre={sesion.nombre} alSalir={salir} />
}

function Login({ alEntrar }: { alEntrar: (sesion: Sesion) => void }) {
  const [correo, setCorreo] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string>()

  async function entrar(evento: FormEvent) {
    evento.preventDefault()
    const { data, error } = await api.POST('/api/auth/login', { body: { correo, password } })
    if (data) alEntrar(data)
    else setError((error as { mensaje?: string } | undefined)?.mensaje ?? 'No se pudo iniciar sesión.')
  }

  return (
    <main>
      <h1>TransitGate</h1>
      <form className="formulario" onSubmit={entrar}>
        <label>
          Correo
          <input type="email" value={correo} onChange={(e) => setCorreo(e.target.value)} autoComplete="username" required />
        </label>
        <label>
          Contraseña
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <p className="error">{error}</p>}
        <button type="submit">Iniciar sesión</button>
      </form>
    </main>
  )
}

function Cliente({ nombre, alSalir }: { nombre: string; alSalir: () => void }) {
  const [tarjeta, setTarjeta] = useState<MiTarjeta>()

  useEffect(() => {
    api.GET('/api/tarjetas/mi-tarjeta').then(({ data }) => setTarjeta(data))
  }, [])

  return (
    <main>
      <h1>TransitGate</h1>
      <p className="nota">
        Portal de prueba · {nombre} · <button onClick={alSalir}>Cerrar sesión</button>
      </p>
      {tarjeta && (
        <ul className="lista">
          <li>
            Saldo: <strong>{tarjeta.saldo.toLocaleString('es-CO')}</strong>
            <br />
            {tarjeta.soporte
              ? `${tarjeta.soporte.tipo === 'fisico' ? 'Tarjeta física' : 'Celular'}: ${tarjeta.soporte.estado}`
              : 'Sin tarjeta física todavía'}
          </li>
        </ul>
      )}
    </main>
  )
}
