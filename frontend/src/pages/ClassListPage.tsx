import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch } from '../api/client'
import { useClasses, useGuardedMutation, useMyHolds } from '../api/queries'
import type { ClassSummary, CreatedHold, MyHold } from '../api/types'
import { ErrorMessage, StatusBadge, StudentIdNotice } from '../components/Messages'
import { toBanglaDigits } from '../format'
import { useStudentId } from '../student/studentId'

export function ClassListPage() {
  const { validId } = useStudentId()
  const classes = useClasses(validId)
  const myHolds = useMyHolds(validId)

  // The student's HELD or CONFIRMED application per class, to link to instead of "Apply".
  const activeByClass = new Map<string, MyHold>()
  for (const hold of myHolds.data ?? []) {
    if ((hold.status === 'HELD' || hold.status === 'CONFIRMED') && !activeByClass.has(hold.classId)) {
      activeByClass.set(hold.classId, hold)
    }
  }

  return (
    <section>
      <div className="page-title">
        <h1>ক্লাস তালিকা</h1>
        <p className="muted">আসনের হিসাব প্রতি ৩ সেকেন্ডে নিজে থেকেই হালনাগাদ হয়।</p>
      </div>

      {classes.isPending && <p className="muted">ক্লাসের তালিকা আনা হচ্ছে…</p>}
      {classes.isError && <ErrorMessage error={classes.error} />}

      {classes.data && classes.data.length === 0 && (
        <div className="card empty">
          <p>এখনো কোনো ক্লাস তৈরি হয়নি।</p>
          <Link to="/admin/classes/new" className="button">
            নতুন ক্লাস তৈরি করুন
          </Link>
        </div>
      )}

      {classes.data && classes.data.length > 0 && (
        <ul className="class-grid">
          {classes.data.map((schoolClass) => (
            <li key={schoolClass.id}>
              <ClassCard schoolClass={schoolClass} activeHold={activeByClass.get(schoolClass.id)} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function ClassCard({ schoolClass, activeHold }: { schoolClass: ClassSummary; activeHold?: MyHold }) {
  const { validId, focusInput } = useStudentId()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [needsId, setNeedsId] = useState(false)

  const apply = useGuardedMutation({
    mutationFn: (studentId: string) =>
      apiFetch<CreatedHold>(`/classes/${schoolClass.id}/holds`, { method: 'POST', studentId }),
    onSuccess: (hold) => navigate(`/holds/${hold.holdId}`),
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['classes'] })
      queryClient.invalidateQueries({ queryKey: ['myHolds'] })
    },
  })

  const full = schoolClass.availableSeats <= 0

  function onApply() {
    if (!validId) {
      setNeedsId(true)
      focusInput()
      return
    }
    setNeedsId(false)
    apply.run(validId)
  }

  return (
    <article className="card class-card">
      <h2>{schoolClass.name}</h2>
      <SeatBar schoolClass={schoolClass} />
      <dl className="seat-stats">
        <div>
          <dt>মোট আসন</dt>
          <dd>{toBanglaDigits(schoolClass.totalSeats)}</dd>
        </div>
        <div>
          <dt>সংরক্ষিত</dt>
          <dd>{toBanglaDigits(schoolClass.heldSeats)}</dd>
        </div>
        <div>
          <dt>নিশ্চিত</dt>
          <dd>{toBanglaDigits(schoolClass.confirmedSeats)}</dd>
        </div>
        <div>
          <dt>খালি</dt>
          <dd className={full ? 'text-danger' : 'text-ok'}>{toBanglaDigits(schoolClass.availableSeats)}</dd>
        </div>
      </dl>

      {activeHold ? (
        <div className="card-action">
          <StatusBadge status={activeHold.status} />
          <Link to={`/holds/${activeHold.holdId}`} className="button button-secondary">
            আপনার আবেদন দেখুন
          </Link>
        </div>
      ) : (
        <div className="card-action">
          <button type="button" className="button" onClick={onApply} disabled={full || apply.isPending}>
            {apply.isPending ? 'আবেদন পাঠানো হচ্ছে…' : full ? 'আসন পূর্ণ' : 'আবেদন করুন'}
          </button>
        </div>
      )}

      {needsId && !validId && <StudentIdNotice action="আবেদন করতে" />}
      {apply.isError && <ErrorMessage error={apply.error} />}
    </article>
  )
}

function SeatBar({ schoolClass }: { schoolClass: ClassSummary }) {
  const { totalSeats, heldSeats, confirmedSeats } = schoolClass
  const percent = (seats: number) => `${Math.min(100, (seats / totalSeats) * 100)}%`

  return (
    <div className="seat-bar" aria-hidden="true">
      <span className="seat-bar-confirmed" style={{ width: percent(confirmedSeats) }} />
      <span className="seat-bar-held" style={{ width: percent(heldSeats) }} />
    </div>
  )
}
