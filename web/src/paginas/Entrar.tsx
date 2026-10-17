import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { api, mensajeDe } from '../api/cliente'
import { esPersonal, useSesion } from '../sesion'

/** RF-02: el mismo inicio de sesión para clientes y personal; cada uno llega a su pantalla. */
export default function Entrar() {
  const { entrar } = useSesion()
  const navegar = useNavigate()
  const desde = (useLocation().state as { desde?: string } | null)?.desde
  const [correo, setCorreo] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string>()
  const [enviando, setEnviando] = useState(false)

  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    setEnviando(true)
    const { data, error } = await api.POST('/api/auth/login', { body: { correo, password } })
    setEnviando(false)
    if (!data) {
      setError(mensajeDe(error, 'No se pudo iniciar sesión.'))
      return
    }
    entrar(data)
    navegar(desde ?? (esPersonal(data) ? '/panel' : '/mi-tarjeta'), { replace: true })
  }

  return (
    <>
      <h1 className="marca">TransitGate</h1>
      <form className="formulario tarjeta" onSubmit={enviar}>
        <h2>Iniciar sesión</h2>
        <label>
          Correo
          <input type="email" value={correo} onChange={(e) => setCorreo(e.target.value)} autoComplete="username" required />
        </label>
        <label>
          Contraseña
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" className="principal" disabled={enviando}>
          {enviando ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
      <p className="nota centrado">
        ¿Primera vez? <Link to="/registro">Crea tu cuenta</Link>
      </p>
    </>
  )
}
