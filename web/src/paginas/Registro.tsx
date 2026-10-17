import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { api, codigoDe, mensajeDe } from '../api/cliente'
import { useSesion } from '../sesion'

type Campo = 'nombreCompleto' | 'tipoDocumento' | 'numeroDocumento' | 'correo' | 'telefono' | 'password' | 'autoriza'

// RF-01 CA3: la API dice con un código qué campo falla, y aquí se marca ese campo.
const campoDelError: Record<string, Campo> = {
  NombreRequerido: 'nombreCompleto',
  TipoDocumentoInvalido: 'tipoDocumento',
  DocumentoInvalido: 'numeroDocumento',
  DocumentoDuplicado: 'numeroDocumento',
  CorreoInvalido: 'correo',
  CorreoDuplicado: 'correo',
  TelefonoInvalido: 'telefono',
  PasswordCorta: 'password',
  PasswordInvalida: 'password',
  AutorizacionRequerida: 'autoriza',
}

/** RF-01: registro en el portal. Pide solo los datos que se usan (RNF-27) y deja la sesión iniciada. */
export default function Registro() {
  const { entrar } = useSesion()
  const navegar = useNavigate()
  const [datos, setDatos] = useState({
    nombreCompleto: '',
    tipoDocumento: 'CC',
    numeroDocumento: '',
    correo: '',
    telefono: '',
    password: '',
  })
  const [autoriza, setAutoriza] = useState(false)
  const [error, setError] = useState<{ campo?: Campo; mensaje: string }>()
  const [enviando, setEnviando] = useState(false)

  const cambiar = (campo: keyof typeof datos) => (e: { target: { value: string } }) => {
    setDatos({ ...datos, [campo]: e.target.value })
    if (error?.campo === campo) setError(undefined)
  }

  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    setEnviando(true)
    const { data, error } = await api.POST('/api/clientes', { body: { ...datos, autorizaTratamientoDatos: autoriza } })
    if (!data) {
      setEnviando(false)
      setError({ campo: campoDelError[codigoDe(error) ?? ''], mensaje: mensajeDe(error, 'No se pudo crear la cuenta.') })
      return
    }

    // La API ya dejó la sesión iniciada (RF-01 CA1).
    const sesion = await api.GET('/api/auth/sesion')
    if (sesion.data) entrar(sesion.data)
    navegar('/mi-tarjeta', { replace: true })
  }

  const errorDe = (campo: Campo) =>
    error?.campo === campo ? <span className="error" role="alert">{error.mensaje}</span> : null

  return (
    <>
      <h1 className="marca">TransitGate</h1>
      <form className="formulario tarjeta" onSubmit={enviar} noValidate>
        <h2>Crea tu cuenta</h2>
        <label>
          Nombre completo
          <input value={datos.nombreCompleto} onChange={cambiar('nombreCompleto')} autoComplete="name" aria-invalid={error?.campo === 'nombreCompleto'} />
          {errorDe('nombreCompleto')}
        </label>
        <div className="fila-campos">
          <label className="campo-corto">
            Documento
            <select value={datos.tipoDocumento} onChange={cambiar('tipoDocumento')} aria-invalid={error?.campo === 'tipoDocumento'}>
              <option value="CC">CC</option>
              <option value="CE">CE</option>
              <option value="TI">TI</option>
              <option value="PP">Pasaporte</option>
            </select>
          </label>
          <label>
            Número
            <input value={datos.numeroDocumento} onChange={cambiar('numeroDocumento')} inputMode={datos.tipoDocumento === 'PP' ? 'text' : 'numeric'} aria-invalid={error?.campo === 'numeroDocumento'} />
          </label>
        </div>
        {errorDe('tipoDocumento')}
        {errorDe('numeroDocumento')}
        <label>
          Correo
          <input type="email" value={datos.correo} onChange={cambiar('correo')} autoComplete="email" aria-invalid={error?.campo === 'correo'} />
          {errorDe('correo')}
        </label>
        <label>
          Celular
          <input type="tel" value={datos.telefono} onChange={cambiar('telefono')} autoComplete="tel-national" inputMode="numeric" placeholder="3001234567" aria-invalid={error?.campo === 'telefono'} />
          {errorDe('telefono')}
        </label>
        <label>
          Contraseña
          <input type="password" value={datos.password} onChange={cambiar('password')} autoComplete="new-password" aria-invalid={error?.campo === 'password'} />
          <span className="nota">Al menos 8 caracteres.</span>
          {errorDe('password')}
        </label>
        <label className="casilla">
          <input type="checkbox" checked={autoriza} onChange={(e) => { setAutoriza(e.target.checked); if (error?.campo === 'autoriza') setError(undefined) }} />
          <span>
            Autorizo el tratamiento de mis datos personales para usar TransitGate: registro, recargas y viajes (Ley 1581 de 2012).
          </span>
        </label>
        {errorDe('autoriza')}
        {error && !error.campo && <p className="error" role="alert">{error.mensaje}</p>}
        <button type="submit" className="principal" disabled={enviando}>
          {enviando ? 'Creando…' : 'Crear cuenta'}
        </button>
      </form>
      <p className="nota centrado">
        ¿Ya tienes cuenta? <Link to="/entrar">Inicia sesión</Link>
      </p>
    </>
  )
}
