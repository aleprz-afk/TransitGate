import { useState } from 'react'
import { prefiereMenosMovimiento } from './movimiento'

// Marca «atardecer llanero»: ciruela para la compuerta y las acciones, coral solo para vías y trenes.

/** Logo: un arco (la compuerta), la vía que lo cruza y una estación en el centro. */
export function Logo() {
  return (
    <svg className="logo" viewBox="0 0 40 40" aria-hidden="true">
      <path className="logo-arco" d="M10 34V16a10 10 0 0 1 20 0v18" />
      <path className="logo-via" d="M2 25h36" />
      <circle className="logo-estacion" cx="20" cy="25" r="5" />
    </svg>
  )
}

// Recorrido de cada línea, su tono y cuánto tarda el tren; el inicio negativo evita que salgan todos a la vez.
const lineas: { d: string; tono: 1 | 2 | 3; segundos: number; inicio: number }[] = [
  { d: 'M-40 110H200L320 230H660L780 110H1240', tono: 1, segundos: 22, inicio: 0 },
  { d: 'M100 -40V330L260 490V840', tono: 2, segundos: 20, inicio: -8 },
  { d: 'M-40 620H360L460 520H860L990 650H1240', tono: 3, segundos: 25, inicio: -5 },
  { d: 'M1060 -40V320L900 480V840', tono: 1, segundos: 20, inicio: -12 },
  { d: 'M-40 390H160L300 530H560L760 730H1240', tono: 2, segundos: 27, inicio: -15 },
]

const estaciones = [
  [160, 110], [480, 230], [860, 110], [1060, 110], [100, 220], [260, 620], [260, 490],
  [660, 520], [1140, 650], [1060, 240], [900, 480], [480, 530], [900, 730],
]

/**
 * Plano de líneas de tren detrás del contenido. Es solo decoración: en la entrada se ve, en el portal va
 * tenue y en las pantallas del personal, además, quieto. Con una alarma se oculta (index.css). Si el sistema
 * pide menos movimiento, no hay trenes.
 */
export function PlanoTrenes({ lugar }: { lugar: 'entrada' | 'portal' | 'personal' }) {
  const [quieto] = useState(prefiereMenosMovimiento)
  const conTrenes = lugar !== 'personal' && !quieto
  return (
    <svg className={`plano ${lugar}`} viewBox="0 0 1200 800" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
      {lineas.map((l) => <path key={l.d} className={`via via-${l.tono}`} d={l.d} />)}
      {estaciones.map(([x, y]) => <circle key={`${x}-${y}`} className="estacion" cx={x} cy={y} r="10" />)}
      {conTrenes && lineas.map((l) => (
        <g key={l.d}>
          <rect className="vagon" x="-30" y="-9" width="60" height="18" rx="9" />
          <rect className="ventana" x="-17" y="-4" width="14" height="8" rx="2" />
          <rect className="ventana" x="4" y="-4" width="14" height="8" rx="2" />
          <animateMotion path={l.d} dur={`${l.segundos}s`} begin={`${l.inicio}s`} rotate="auto" repeatCount="indefinite" />
        </g>
      ))}
    </svg>
  )
}
