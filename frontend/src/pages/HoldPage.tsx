import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch } from '../api/client'
import { queryKeys, useClass, useGuardedMutation, useHold } from '../api/queries'
import type { ConfirmedHold, HoldStatus, TimedHold } from '../api/types'
import { ErrorMessage, StatusBadge, StudentIdNotice, SuccessMessage } from '../components/Messages'
import { formatCountdown, formatDateTime } from '../format'
import { useStudentId } from '../student/studentId'
import { useNow } from '../useNow'

export function HoldPage() {
  const { holdId = '' } = useParams()
  const { validId, focusInput } = useStudentId()
  const queryClient = useQueryClient()
  const hold = useHold(holdId, validId)
  const schoolClass = useClass(hold.data?.classId, validId)
  const [done, setDone] = useState<'paid' | 'cancelled' | null>(null)

  // Counts down from the server's secondsRemaining, measured from when the request
  // started, so the browser's own clock setting does not matter.
  const data = hold.data
  const ticking = data?.status === 'HELD'
  const now = useNow(ticking)
  const remaining = data && ticking ? secondsLeft(data, now) : 0
  const status: HoldStatus | undefined = ticking && remaining === 0 ? 'EXPIRED' : data?.status
  const expiredLocally = ticking && remaining === 0

  // At zero, ask the server once for the real status; it reports EXPIRED by then.
  const { refetch } = hold
  const requestedAt = data?.requestedAt
  useEffect(() => {
    if (expiredLocally) refetch()
  }, [expiredLocally, requestedAt, refetch])

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hold', holdId] })
    queryClient.invalidateQueries({ queryKey: ['classes'] })
    queryClient.invalidateQueries({ queryKey: ['class'] })
    queryClient.invalidateQueries({ queryKey: ['myHolds'] })
  }

  const pay = useGuardedMutation({
    mutationFn: (studentId: string) =>
      apiFetch<ConfirmedHold>(`/holds/${encodeURIComponent(holdId)}/confirm`, { method: 'POST', studentId }),
    onSuccess: (confirmed) => {
      setDone('paid')
      queryClient.setQueryData<TimedHold>(queryKeys.hold(holdId, validId), (old) =>
        old ? { ...old, status: confirmed.status, secondsRemaining: 0 } : old,
      )
    },
    onSettled: refresh,
  })

  const cancel = useGuardedMutation({
    mutationFn: (studentId: string) =>
      apiFetch<void>(`/holds/${encodeURIComponent(holdId)}`, { method: 'DELETE', studentId }),
    onSuccess: () => {
      setDone('cancelled')
      queryClient.setQueryData<TimedHold>(queryKeys.hold(holdId, validId), (old) =>
        old ? { ...old, status: 'CANCELLED', secondsRemaining: 0 } : old,
      )
    },
    onSettled: refresh,
  })

  const busy = pay.isPending || cancel.isPending
  const actionable = status === 'HELD'

  function onPay() {
    if (!validId) return focusInput()
    cancel.reset()
    pay.run(validId)
  }

  function onCancel() {
    if (!validId) return focusInput()
    if (!window.confirm('আবেদনটি বাতিল করতে চান? বাতিল করলে আসনটি অন্যরা পেয়ে যেতে পারে।')) return
    pay.reset()
    cancel.run(validId)
  }

  if (hold.isPending) return <p className="muted">আবেদনের তথ্য আনা হচ্ছে…</p>

  if (!data) {
    return (
      <section className="narrow">
        <h1>আবেদন</h1>
        <ErrorMessage error={hold.error} />
        <p>
          <Link to="/me">আমার আবেদনগুলো দেখুন →</Link>
        </p>
      </section>
    )
  }

  return (
    <section className="narrow">
      <div className="page-title">
        <h1>আবেদন</h1>
        <p className="muted">{schoolClass.data?.name ?? 'ক্লাসের নাম আনা হচ্ছে…'}</p>
      </div>

      <div className="card hold-card">
        <div className="hold-status">
          <span className="muted">অবস্থা</span>
          {status && <StatusBadge status={status} />}
        </div>

        {status === 'HELD' && (
          <div className="countdown" aria-live="off">
            <span className="muted">ফি দেওয়ার বাকি সময়</span>
            <strong className={remaining <= 60 ? 'text-danger' : undefined}>{formatCountdown(remaining)}</strong>
          </div>
        )}
        {status === 'EXPIRED' && (
          <p className="message message-error" role="alert">
            সময় শেষ, আসনটি ছেড়ে দেওয়া হয়েছে। চাইলে ক্লাস তালিকা থেকে আবার আবেদন করুন।
          </p>
        )}
        {status === 'CONFIRMED' && <p className="text-ok">ভর্তি ফি পরিশোধ হয়েছে, ভর্তি নিশ্চিত।</p>}
        {status === 'CANCELLED' && <p className="muted">এই আবেদনটি বাতিল করা হয়েছে।</p>}

        {data.status === 'HELD' && (
          <p className="muted small">মেয়াদ শেষ হবে: {formatDateTime(data.expiresAt)}</p>
        )}

        <div className="actions">
          <button type="button" className="button" onClick={onPay} disabled={!actionable || busy}>
            {pay.isPending ? 'ফি দেওয়া হচ্ছে…' : 'ভর্তি ফি দিন'}
          </button>
          <button type="button" className="button button-danger" onClick={onCancel} disabled={!actionable || busy}>
            {cancel.isPending ? 'বাতিল হচ্ছে…' : 'বাতিল'}
          </button>
        </div>

        {actionable && !validId && <StudentIdNotice action="ভর্তি ফি দিতে বা আবেদন বাতিল করতে" />}
        {pay.isError && <ErrorMessage error={pay.error} />}
        {cancel.isError && <ErrorMessage error={cancel.error} />}
        {hold.isError && <ErrorMessage error={hold.error} />}
        {done === 'paid' && <SuccessMessage>অভিনন্দন! ভর্তি ফি পরিশোধ হয়েছে এবং ভর্তি নিশ্চিত হয়েছে।</SuccessMessage>}
        {done === 'cancelled' && <SuccessMessage>আবেদনটি বাতিল করা হয়েছে, আসনটি ছেড়ে দেওয়া হয়েছে।</SuccessMessage>}
      </div>

      <p className="links">
        <Link to="/me">আমার সব আবেদন →</Link>
        <Link to="/">ক্লাস তালিকা →</Link>
      </p>
    </section>
  )
}

function secondsLeft(hold: TimedHold, now: number): number {
  const deadline = hold.requestedAt + hold.secondsRemaining * 1000
  return Math.max(0, Math.min(hold.secondsRemaining, Math.ceil((deadline - now) / 1000)))
}
