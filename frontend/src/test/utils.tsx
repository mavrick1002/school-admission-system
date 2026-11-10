import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { StudentIdProvider } from '../student/StudentIdProvider'

type Reply = { status?: number; body?: unknown } | 'network-error' | 'pending'
type Handler = (request: { method: string; path: string; headers: Record<string, string> }) => Reply | undefined

/**
 * Replaces fetch for the test. The handler answers by method and path; anything it
 * does not answer fails the test, so nothing can reach a real backend.
 */
export function mockFetch(handler: Handler) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input))
    const method = init?.method ?? 'GET'
    const headers = (init?.headers ?? {}) as Record<string, string>
    const reply = handler({ method, path: url.pathname, headers })

    if (reply === undefined) throw new Error(`Unexpected request in test: ${method} ${url.pathname}`)
    if (reply === 'network-error') throw new TypeError('Failed to fetch')
    if (reply === 'pending') return new Promise<Response>(() => {})

    const status = reply.status ?? 200
    const body = reply.body === undefined || status === 204 ? null : JSON.stringify(reply.body)
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/** Renders one screen at `url`, with the app's providers and a route for `path`. */
export function renderRoute(element: ReactElement, { path, url }: { path: string; url: string }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <StudentIdProvider>
        <MemoryRouter initialEntries={[url]}>
          <Routes>
            <Route path={path} element={element} />
            <Route path="*" element={<p>other page</p>} />
          </Routes>
        </MemoryRouter>
      </StudentIdProvider>
    </QueryClientProvider>,
  )
}

export const VALID_ID = '20152691234567890'

export function classSummary(overrides: Partial<{
  id: string
  name: string
  totalSeats: number
  heldSeats: number
  confirmedSeats: number
  availableSeats: number
}> = {}) {
  return {
    id: 'class-1',
    name: 'Class 6 - Morning Shift',
    totalSeats: 3,
    heldSeats: 0,
    confirmedSeats: 0,
    availableSeats: 3,
    ...overrides,
  }
}
