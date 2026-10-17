import { BrowserRouter, Navigate, NavLink, Outlet, Route, Routes, useLocation } from 'react-router'
import Entrar from './paginas/Entrar'
import Registro from './paginas/Registro'
import MiTarjeta from './paginas/cliente/MiTarjeta'
import Perfil from './paginas/cliente/Perfil'
import Recargar from './paginas/cliente/Recargar'
import { Recargas, Viajes } from './paginas/cliente/Historial'
import Panel from './paginas/personal/Panel'
import Taquilla from './paginas/personal/Taquilla'
import { esPersonal, SesionProvider, useSesion } from './sesion'
import { TiempoReal, useEstadoConexion } from './tiempoReal'

// Portal del cliente y panel de operación (Contratos de integración, C6). Cada ruta pide su rol; la API
// lo vuelve a comprobar (RF-06), así que esto solo decide qué pantalla mostrar.

export default function App() {
  return (
    <BrowserRouter>
      <SesionProvider>
        <Routes>
          <Route index element={<Inicio />} />
          <Route path="entrar" element={<SinSesion><Entrar /></SinSesion>} />
          <Route path="registro" element={<SinSesion><Registro /></SinSesion>} />

          <Route element={<ConSesion rol="cliente" />}>
            <Route path="mi-tarjeta" element={<MiTarjeta />} />
            <Route path="recargar" element={<Recargar />} />
            <Route path="viajes" element={<Viajes />} />
            <Route path="recargas" element={<Recargas />} />
            <Route path="perfil" element={<Perfil />} />
          </Route>

          <Route element={<ConSesion rol="personal" />}>
            <Route path="panel" element={<Panel />} />
            <Route path="taquilla" element={<Taquilla />} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </SesionProvider>
    </BrowserRouter>
  )
}

/** La página de inicio de cada rol (RF-02 CA1 y CA2). */
function Inicio() {
  const { sesion } = useSesion()
  if (sesion === undefined) return <Cargando />
  if (sesion === null) return <Navigate to="/entrar" replace />
  return <Navigate to={esPersonal(sesion) ? '/panel' : '/mi-tarjeta'} replace />
}

function SinSesion({ children }: { children: React.ReactNode }) {
  const { sesion } = useSesion()
  if (sesion === undefined) return <Cargando />
  if (sesion) return <Navigate to="/" replace />
  return <main className="contenido angosto">{children}</main>
}

function ConSesion({ rol }: { rol: 'cliente' | 'personal' }) {
  const { sesion } = useSesion()
  const ubicacion = useLocation()

  if (sesion === undefined) return <Cargando />
  if (sesion === null) return <Navigate to="/entrar" replace state={{ desde: ubicacion.pathname }} />
  if ((rol === 'personal') !== esPersonal(sesion)) return <Navigate to="/" replace />   // RF-06 CA2

  return (
    <TiempoReal>
      <Marco rol={rol} />
    </TiempoReal>
  )
}

const enlaces = {
  cliente: [
    ['/mi-tarjeta', 'Mi tarjeta'],
    ['/recargar', 'Recargar'],
    ['/viajes', 'Viajes'],
    ['/recargas', 'Recargas'],
    ['/perfil', 'Perfil'],
  ],
  personal: [
    ['/panel', 'Panel'],
    ['/taquilla', 'Taquilla'],
  ],
} as const

function Marco({ rol }: { rol: 'cliente' | 'personal' }) {
  const { sesion, salir } = useSesion()
  const conexion = useEstadoConexion()

  return (
    <>
      <header className="encabezado">
        <div className="encabezado-fila">
          <strong className="marca">TransitGate</strong>
          <span className="nota">
            {sesion?.nombre}
            {rol === 'personal' && conexion !== 'Conectado' && <span className="etiqueta aviso">{conexion}</span>}
          </span>
          <button className="enlace" onClick={() => void salir()}>
            Cerrar sesión
          </button>
        </div>
        <nav className="navegacion">
          {enlaces[rol].map(([ruta, texto]) => (
            <NavLink key={ruta} to={ruta}>
              {texto}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="contenido">
        <Outlet />
      </main>
    </>
  )
}

export function Cargando() {
  return <p className="nota contenido">Cargando…</p>
}
