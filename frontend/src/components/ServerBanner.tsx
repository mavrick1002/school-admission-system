import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { apiFetch } from '../api/client'
import { useServerReachable } from '../api/serverStatus'

const PROBE_MS = 5000

/** Shown while the backend cannot be reached; probes /health until it answers again. */
export function ServerBanner() {
  const reachable = useServerReachable()
  const queryClient = useQueryClient()

  useEffect(() => {
    if (reachable) return
    const id = setInterval(() => {
      apiFetch('/health').catch(() => {})
    }, PROBE_MS)
    return () => clearInterval(id)
  }, [reachable])

  // Once the server is back after an outage, bring every screen up to date at once.
  const wasDown = useRef(false)
  useEffect(() => {
    if (!reachable) {
      wasDown.current = true
    } else if (wasDown.current) {
      wasDown.current = false
      queryClient.invalidateQueries()
    }
  }, [reachable, queryClient])

  if (reachable) return null

  return (
    <div className="server-banner" role="alert">
      <span>
        সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। কিছুক্ষণ পরপর স্বয়ংক্রিয়ভাবে আবার চেষ্টা করা হচ্ছে।
      </span>
      <button type="button" className="button button-small" onClick={() => queryClient.refetchQueries({ type: 'active' })}>
        এখনই চেষ্টা করুন
      </button>
    </div>
  )
}
