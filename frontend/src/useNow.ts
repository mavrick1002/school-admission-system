import { useEffect, useState } from 'react'

/** performance.now(), re-read every intervalMs while active, so a countdown re-renders. */
export function useNow(active: boolean, intervalMs = 250): number {
  const [now, setNow] = useState(() => performance.now())

  useEffect(() => {
    if (!active) return
    const tick = () => setNow(performance.now())
    tick()
    const id = setInterval(tick, intervalMs)
    return () => clearInterval(id)
  }, [active, intervalMs])

  return now
}
