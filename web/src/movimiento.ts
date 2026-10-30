import { useEffect, useRef, useState } from 'react'

// Animaciones con propósito: cada una muestra algo que pasó en vivo (saldo, paso abierto, alarma).
// Si el sistema pide menos movimiento, todo cambia de una vez, sin animar.

export const prefiereMenosMovimiento = () =>
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Valor que avanza hasta el nuevo en ~0,7 s cuando cambia (el saldo que baja con un paso o sube con una
 * recarga). El primer valor se muestra tal cual.
 */
export function useNumeroAnimado(valor: number, duracionMs = 700): number {
  const [mostrado, setMostrado] = useState(valor)
  const desde = useRef(valor)

  useEffect(() => {
    const inicio = desde.current
    desde.current = valor
    if (inicio === valor || prefiereMenosMovimiento() || document.hidden) {
      setMostrado(valor)
      return
    }
    const t0 = performance.now()
    let marco = 0
    const paso = (ahora: number) => {
      const k = Math.min(1, (ahora - t0) / duracionMs)
      const suave = 1 - Math.pow(1 - k, 3)   // rápido al principio, frena al llegar
      setMostrado(Math.round(inicio + (valor - inicio) * suave))
      if (k < 1) marco = requestAnimationFrame(paso)
    }
    marco = requestAnimationFrame(paso)
    // El navegador pausa requestAnimationFrame cuando no dibuja (pestaña o panel oculto): sin este respaldo el
    // saldo se quedaría en el valor viejo. Pase lo que pase con la animación, al terminar queda el valor real.
    const respaldo = setTimeout(() => {
      cancelAnimationFrame(marco)
      setMostrado(valor)
    }, duracionMs + 50)
    return () => {
      cancelAnimationFrame(marco)
      clearTimeout(respaldo)
    }
  }, [valor, duracionMs])

  return mostrado
}

/** Vibración corta en el celular del personal cuando llega un colado (donde el navegador lo permite). */
export function vibrarAlarma() {
  if (!prefiereMenosMovimiento()) navigator.vibrate?.([200, 100, 200])
}
