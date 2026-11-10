import { Link } from 'react-router-dom'
import type { HoldStatus } from '../api/types'
import { errorMessage, isApiError } from '../errors'
import { useStudentId } from '../student/studentId'

/** The Bangla message for any error; ALREADY_APPLIED also links to the existing application. */
export function ErrorMessage({ error }: { error: unknown }) {
  const existingHoldId = isApiError(error, 'ALREADY_APPLIED') ? error.holdId : undefined

  return (
    <div className="message message-error" role="alert">
      <p>{errorMessage(error)}</p>
      {existingHoldId && (
        <p>
          <Link to={`/holds/${existingHoldId}`}>আগের আবেদনটি দেখুন →</Link>
        </p>
      )}
    </div>
  )
}

export function SuccessMessage({ children }: { children: React.ReactNode }) {
  return (
    <div className="message message-success" role="status">
      {children}
    </div>
  )
}

/** Explains that an action needs the student ID, with a button that jumps to the box. */
export function StudentIdNotice({ action }: { action: string }) {
  const { focusInput } = useStudentId()

  return (
    <div className="message message-warning" role="alert">
      <p>{action} উপরের বক্সে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</p>
      <button type="button" className="button button-small button-secondary" onClick={focusInput}>
        নম্বর লিখুন
      </button>
    </div>
  )
}

const STATUS_LABELS: Record<HoldStatus, string> = {
  HELD: 'আসন সংরক্ষিত, ফি বাকি',
  CONFIRMED: 'ভর্তি নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিল',
}

export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge-${status.toLowerCase()}`}>
      {STATUS_LABELS[status] ?? status} · {status}
    </span>
  )
}
