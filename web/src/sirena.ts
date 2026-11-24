import { useEffect, useState } from 'react'

// Sirena del panel: suena en el navegador mientras haya una alerta de colado abierta (apoya a RF-33 cuando la
// sirena física no está). Usa Web Audio, así que no necesita archivos de sonido.

const TONOS_HZ = [700, 1100]
const CAMBIO_MS = 450
const VOLUMEN = 0.15

let contexto: AudioContext | undefined

function obtenerContexto(): AudioContext {
  contexto ??= new AudioContext()
  return contexto
}

/**
 * Hace sonar la sirena mientras `activa` sea true. El navegador no deja reproducir sonido hasta que el usuario
 * toque la página: en ese caso devuelve `bloqueada` y `activar` (para un botón); con cualquier clic se desbloquea.
 */
export function useSirena(activa: boolean) {
  const [bloqueada, setBloqueada] = useState(false)
  const [desbloqueos, setDesbloqueos] = useState(0)

  useEffect(() => {
    if (!activa) {
      setBloqueada(false)
      return
    }
    const ctx = obtenerContexto()
    if (ctx.state !== 'running') {
      void ctx.resume().catch(() => {})
    }

    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = 'square'
    osc.frequency.value = TONOS_HZ[0]
    gain.gain.value = VOLUMEN
    osc.connect(gain).connect(ctx.destination)
    osc.start()
    let tono = 0
    const timer = window.setInterval(() => {
      tono = (tono + 1) % TONOS_HZ.length
      osc.frequency.setValueAtTime(TONOS_HZ[tono], ctx.currentTime)
      setBloqueada(ctx.state !== 'running')
    }, CAMBIO_MS)
    setBloqueada(ctx.state !== 'running')

    // Si el navegador bloqueó el audio, el primer clic en cualquier parte lo desbloquea.
    const desbloquear = () => void ctx.resume().then(() => setBloqueada(false)).catch(() => {})
    document.addEventListener('pointerdown', desbloquear)

    return () => {
      document.removeEventListener('pointerdown', desbloquear)
      window.clearInterval(timer)
      osc.stop()
      osc.disconnect()
      gain.disconnect()
    }
  }, [activa, desbloqueos])

  function activar() {
    void obtenerContexto()
      .resume()
      .then(() => {
        setBloqueada(false)
        setDesbloqueos((n) => n + 1)
      })
      .catch(() => {})
  }

  return { bloqueada, activar }
}
