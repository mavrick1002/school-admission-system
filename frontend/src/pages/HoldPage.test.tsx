import { act, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { classSummary, mockFetch, renderRoute } from '../test/utils'
import { HoldPage } from './HoldPage'

const heldHold = {
  holdId: 'hold-1',
  classId: 'class-1',
  status: 'HELD',
  expiresAt: '2026-11-10T10:00:03.000Z',
  secondsRemaining: 3,
}

describe('application page countdown', () => {
  it('shows EXPIRED and disables Pay when the countdown reaches 0, without a reload', async () => {
    // performance is faked too, since the countdown is measured with performance.now().
    vi.useFakeTimers({
      toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date', 'performance'],
      shouldAdvanceTime: true,
    })

    let holdCalls = 0
    mockFetch(({ method, path }) => {
      if (method === 'GET' && path === '/holds/hold-1') {
        holdCalls++
        // The first answer starts the countdown. Later re-checks never answer, so
        // EXPIRED below can only come from the countdown itself.
        return holdCalls === 1 ? { body: heldHold } : 'pending'
      }
      if (method === 'GET' && path === '/classes/class-1') return { body: classSummary() }
      return undefined
    })

    renderRoute(<HoldPage />, { path: '/holds/:holdId', url: '/holds/hold-1' })

    expect(await screen.findByText('০০:০৩')).toBeInTheDocument()
    expect(screen.getByText('আসন সংরক্ষিত, ফি বাকি · HELD')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeEnabled()
    expect(screen.getByText(/^মেয়াদ শেষ হবে:/)).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })
    expect(screen.getByText('০০:০২')).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2500)
    })

    expect(screen.getByText('মেয়াদ শেষ · EXPIRED')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'বাতিল' })).toBeDisabled()
    expect(screen.getByText(/সময় শেষ, আসনটি ছেড়ে দেওয়া হয়েছে।/)).toBeInTheDocument()
    expect(screen.queryByText('ফি দেওয়ার বাকি সময়')).not.toBeInTheDocument()
    expect(screen.queryByText(/^মেয়াদ শেষ হবে:/)).not.toBeInTheDocument()
  })
})
