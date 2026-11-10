import { useSyncExternalStore } from 'react'

// Whether the last request reached the backend. Every apiFetch call reports here.
let reachable = true
const listeners = new Set<() => void>()

export function reportReachable(value: boolean) {
  if (value === reachable) return
  reachable = value
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function useServerReachable() {
  return useSyncExternalStore(subscribe, () => reachable)
}
