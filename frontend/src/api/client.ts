import { API_BASE_URL } from '../config'
import { reportReachable } from './serverStatus'

/** The backend answered with an error body: { code, message } (plus holdId for ALREADY_APPLIED). */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly holdId?: string

  constructor(status: number, code: string, message: string, holdId?: string) {
    super(message || code)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.holdId = holdId
  }
}

/** The backend could not be reached at all (down, wrong URL, timeout, CORS failure). */
export class NetworkError extends Error {
  constructor() {
    super('The server could not be reached.')
    this.name = 'NetworkError'
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE'
  studentId?: string | null
  body?: unknown
  signal?: AbortSignal
}

const TIMEOUT_MS = 15_000

export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', studentId, body, signal } = options

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (studentId) headers['X-Student-Id'] = studentId
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  const timeout = AbortSignal.timeout(TIMEOUT_MS)
  let response: Response
  try {
    response = await fetch(API_BASE_URL + path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
    })
  } catch (error) {
    // A query cancelled by TanStack Query is not an outage.
    if (signal?.aborted) throw error
    reportReachable(false)
    throw new NetworkError()
  }
  reportReachable(true)

  if (response.status === 204) return undefined as T

  const text = await response.text()
  let data: unknown
  try {
    data = text ? JSON.parse(text) : null
  } catch {
    data = null
  }

  if (!response.ok) {
    const error = (data ?? {}) as { code?: string; message?: string; holdId?: string }
    throw new ApiError(
      response.status,
      error.code ?? `HTTP_${response.status}`,
      error.message ?? '',
      error.holdId,
    )
  }

  return data as T
}
