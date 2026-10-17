import { useEffect, useState, type FormEvent } from 'react'
import { api, mensajeDe, type Esquema } from '../../api/cliente'
import { Cargando } from '../../App'

/** RF-05: datos del cliente. Solo se cambia el teléfono; el documento lo identifica en taquilla (CA3). */
export default function Perfil() {
  const [perfil, setPerfil] = useState<Esquema['PerfilCliente']>()
  const [telefono, setTelefono] = useState('')
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'error'; texto: string }>()

  useEffect(() => {
    api.GET('/api/clientes/me').then(({ data }) => {
      if (!data) return
      setPerfil(data)
      setTelefono(data.telefono)
    })
  }, [])

  async function guardar(evento: FormEvent) {
    evento.preventDefault()
    const { error, response } = await api.PATCH('/api/clientes/me', { body: { telefono } })
    setAviso(response.ok ? { tipo: 'ok', texto: 'Teléfono actualizado.' } : { tipo: 'error', texto: mensajeDe(error) })
  }

  if (!perfil) return <Cargando />

  return (
    <>
      <h1>Tu perfil</h1>
      <dl className="tarjeta datos">
        <dt>Nombre</dt>
        <dd>{perfil.nombreCompleto}</dd>
        <dt>Documento</dt>
        <dd>{perfil.tipoDocumento} {perfil.numeroDocumento}</dd>
        <dt>Correo</dt>
        <dd>{perfil.correo}</dd>
      </dl>
      <form className="formulario tarjeta" onSubmit={guardar}>
        <label>
          Celular
          <input type="tel" inputMode="numeric" value={telefono} onChange={(e) => { setTelefono(e.target.value); setAviso(undefined) }} />
        </label>
        {aviso && <p className={aviso.tipo === 'ok' ? 'exito' : 'error'} role="status">{aviso.texto}</p>}
        <button type="submit">Guardar</button>
      </form>
    </>
  )
}
