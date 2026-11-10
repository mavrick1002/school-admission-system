import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { NETWORK_MESSAGE } from '../errors'
import { VALID_ID, classSummary, mockFetch, renderRoute } from '../test/utils'
import { ClassListPage } from './ClassListPage'

function renderList() {
  return renderRoute(<ClassListPage />, { path: '/', url: '/' })
}

async function cardFor(name: string) {
  const heading = await screen.findByRole('heading', { name })
  return within(heading.closest('article')!)
}

describe('class list', () => {
  it('disables Apply when a class is full and keeps it enabled otherwise', async () => {
    mockFetch(({ path }) =>
      path === '/classes'
        ? {
            body: [
              classSummary({ id: 'full', name: 'Full class', totalSeats: 2, heldSeats: 1, confirmedSeats: 1, availableSeats: 0 }),
              classSummary({ id: 'open', name: 'Open class' }),
            ],
          }
        : undefined,
    )
    renderList()

    const full = await cardFor('Full class')
    expect(full.getByRole('button', { name: 'আসন পূর্ণ' })).toBeDisabled()
    const open = await cardFor('Open class')
    expect(open.getByRole('button', { name: 'আবেদন করুন' })).toBeEnabled()
  })

  it('asks for the student ID when Apply is pressed without one, and sends nothing', async () => {
    const fetchMock = mockFetch(({ path }) => (path === '/classes' ? { body: [classSummary()] } : undefined))
    renderList()

    const card = await cardFor('Class 6 - Morning Shift')
    await userEvent.click(card.getByRole('button', { name: 'আবেদন করুন' }))

    expect(card.getByText(/আবেদন করতে উপরের বক্সে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।/)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('links to the existing application on ALREADY_APPLIED', async () => {
    localStorage.setItem('admission.studentId', VALID_ID)
    mockFetch(({ method, path, headers }) => {
      if (method === 'GET' && path === '/classes') return { body: [classSummary()] }
      if (method === 'GET' && path === '/me/holds') return { body: [] }
      if (method === 'POST' && path === '/classes/class-1/holds') {
        expect(headers['X-Student-Id']).toBe(VALID_ID)
        return {
          status: 409,
          body: { code: 'ALREADY_APPLIED', message: 'Already applied.', holdId: 'hold-existing' },
        }
      }
      return undefined
    })
    renderList()

    const card = await cardFor('Class 6 - Morning Shift')
    await userEvent.click(card.getByRole('button', { name: 'আবেদন করুন' }))

    expect(
      await card.findByText('এই ক্লাসে এই শিক্ষার্থীর একটি চলমান আবেদন বা নিশ্চিত ভর্তি আগে থেকেই আছে।'),
    ).toBeInTheDocument()
    expect(card.getByRole('link', { name: 'আগের আবেদনটি দেখুন →' })).toHaveAttribute('href', '/holds/hold-existing')
  })

  it('shows the Bangla seats-full message on CLASS_FULL', async () => {
    localStorage.setItem('admission.studentId', VALID_ID)
    mockFetch(({ method, path }) => {
      if (method === 'GET' && path === '/classes') return { body: [classSummary({ availableSeats: 1 })] }
      if (method === 'GET' && path === '/me/holds') return { body: [] }
      if (method === 'POST' && path === '/classes/class-1/holds') {
        return { status: 409, body: { code: 'CLASS_FULL', message: 'There are no seats left in this class.' } }
      }
      return undefined
    })
    renderList()

    const card = await cardFor('Class 6 - Morning Shift')
    await userEvent.click(card.getByRole('button', { name: 'আবেদন করুন' }))

    expect(await card.findByText('দুঃখিত, এই ক্লাসে আর কোনো আসন খালি নেই।')).toBeInTheDocument()
    expect(card.queryByRole('link', { name: 'আগের আবেদনটি দেখুন →' })).not.toBeInTheDocument()
  })

  it('shows the Bangla network message when the server cannot be reached', async () => {
    mockFetch(() => 'network-error')
    renderList()

    expect(await screen.findByText(NETWORK_MESSAGE)).toBeInTheDocument()
    expect(NETWORK_MESSAGE).toMatch(/^সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না।/)
  })

  it('shows the network message when Apply cannot reach the server', async () => {
    localStorage.setItem('admission.studentId', VALID_ID)
    mockFetch(({ method, path }) => {
      if (method === 'GET' && path === '/classes') return { body: [classSummary()] }
      if (method === 'GET' && path === '/me/holds') return { body: [] }
      if (method === 'POST') return 'network-error'
      return undefined
    })
    renderList()

    const card = await cardFor('Class 6 - Morning Shift')
    await userEvent.click(card.getByRole('button', { name: 'আবেদন করুন' }))

    expect(await card.findByText(NETWORK_MESSAGE)).toBeInTheDocument()
  })

  it('shows the server-error message when GET /classes does not return an array', async () => {
    mockFetch(({ path }) => (path === '/classes' ? { body: { oops: true } } : undefined))
    renderList()

    expect(await screen.findByText('সার্ভারে একটি সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।')).toBeInTheDocument()
    expect(screen.queryByText('এখনো কোনো ক্লাস তৈরি হয়নি।')).not.toBeInTheDocument()
  })
})
