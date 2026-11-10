import { useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api/client'
import { useGuardedMutation } from '../api/queries'
import type { CreatedClass } from '../api/types'
import { ErrorMessage, SuccessMessage } from '../components/Messages'
import { toAsciiDigits, toBanglaDigits } from '../format'

const MAX_NAME_LENGTH = 200
const MAX_SEATS = 2_147_483_647 // the backend stores an int

interface FieldErrors {
  name?: string
  totalSeats?: string
}

function validate(name: string, seatsText: string): { errors: FieldErrors; seats: number } {
  const errors: FieldErrors = {}
  const trimmed = name.trim()
  if (!trimmed) errors.name = 'ক্লাসের নাম দিন।'
  else if (trimmed.length > MAX_NAME_LENGTH) errors.name = `নাম ${toBanglaDigits(MAX_NAME_LENGTH)} অক্ষরের মধ্যে রাখুন।`

  const digits = toAsciiDigits(seatsText.trim())
  const seats = Number(digits)
  if (!digits) errors.totalSeats = 'আসনসংখ্যা দিন।'
  else if (!/^[0-9]+$/.test(digits) || seats < 1) errors.totalSeats = 'আসনসংখ্যা ১ বা তার বেশি একটি পূর্ণ সংখ্যা হতে হবে।'
  else if (seats > MAX_SEATS) errors.totalSeats = 'আসনসংখ্যা অনেক বড় হয়ে গেছে।'

  return { errors, seats }
}

export function CreateClassPage() {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [seatsText, setSeatsText] = useState('')
  const [errors, setErrors] = useState<FieldErrors>({})
  const [created, setCreated] = useState<CreatedClass | null>(null)

  const create = useGuardedMutation({
    mutationFn: (body: { name: string; totalSeats: number }) =>
      apiFetch<CreatedClass>('/classes', { method: 'POST', body }),
    onSuccess: (schoolClass) => {
      setCreated(schoolClass)
      setName('')
      setSeatsText('')
      queryClient.invalidateQueries({ queryKey: ['classes'] })
    },
  })

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    const { errors: found, seats } = validate(name, seatsText)
    setErrors(found)
    if (found.name || found.totalSeats) return
    setCreated(null)
    create.run({ name: name.trim(), totalSeats: seats })
  }

  return (
    <section className="narrow">
      <div className="page-title">
        <h1>নতুন ক্লাস তৈরি (অ্যাডমিন)</h1>
        <p className="muted">প্রতিটি শিফট আলাদা ক্লাস হিসেবে তৈরি করুন, যেমন “Class 6 - Morning Shift”।</p>
      </div>

      <form className="card form" onSubmit={onSubmit} noValidate>
        <div className="field">
          <label htmlFor="class-name">ক্লাসের নাম</label>
          <input
            id="class-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="যেমন: Class 6 - Morning Shift"
            aria-invalid={!!errors.name}
            aria-describedby={errors.name ? 'class-name-error' : undefined}
          />
          {errors.name && (
            <small id="class-name-error" className="hint hint-error">
              {errors.name}
            </small>
          )}
        </div>

        <div className="field">
          <label htmlFor="total-seats">মোট আসন</label>
          <input
            id="total-seats"
            value={seatsText}
            onChange={(event) => setSeatsText(toAsciiDigits(event.target.value))}
            inputMode="numeric"
            placeholder="যেমন: 120"
            aria-invalid={!!errors.totalSeats}
            aria-describedby={errors.totalSeats ? 'total-seats-error' : undefined}
          />
          {errors.totalSeats && (
            <small id="total-seats-error" className="hint hint-error">
              {errors.totalSeats}
            </small>
          )}
        </div>

        <button type="submit" className="button" disabled={create.isPending}>
          {create.isPending ? 'তৈরি হচ্ছে…' : 'ক্লাস তৈরি করুন'}
        </button>

        {create.isError && <ErrorMessage error={create.error} />}
        {created && (
          <SuccessMessage>
            <p>
              “{created.name}” ক্লাসটি তৈরি হয়েছে, মোট আসন {toBanglaDigits(created.totalSeats)}টি।
            </p>
            <p>
              <Link to="/">ক্লাস তালিকায় দেখুন →</Link>
            </p>
          </SuccessMessage>
        )}
      </form>
    </section>
  )
}
