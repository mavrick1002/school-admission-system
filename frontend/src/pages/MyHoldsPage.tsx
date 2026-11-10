import { Link } from 'react-router-dom'
import { useMyHolds } from '../api/queries'
import { ErrorMessage, StatusBadge, StudentIdNotice } from '../components/Messages'
import { formatDateTime } from '../format'
import { useStudentId } from '../student/studentId'

export function MyHoldsPage() {
  const { validId } = useStudentId()
  const holds = useMyHolds(validId)

  return (
    <section className="narrow">
      <div className="page-title">
        <h1>আমার আবেদন</h1>
        <p className="muted">এই শিক্ষার্থীর সব আবেদন, নতুনগুলো আগে।</p>
      </div>

      {!validId && <StudentIdNotice action="আবেদনগুলো দেখতে" />}

      {validId && holds.isPending && <p className="muted">আবেদনগুলো আনা হচ্ছে…</p>}
      {validId && holds.isError && <ErrorMessage error={holds.error} />}

      {validId && holds.data?.length === 0 && (
        <div className="card empty">
          <p>এই শিক্ষার্থীর এখনো কোনো আবেদন নেই।</p>
          <Link to="/" className="button">
            ক্লাস তালিকা থেকে আবেদন করুন
          </Link>
        </div>
      )}

      {validId && holds.data && holds.data.length > 0 && (
        <ul className="hold-list">
          {holds.data.map((hold) => (
            <li key={hold.holdId} className="card hold-row">
              <div>
                <h2>{hold.className}</h2>
                <StatusBadge status={hold.status} />
                {hold.status === 'HELD' && (
                  <p className="muted small">মেয়াদ শেষ হবে: {formatDateTime(hold.expiresAt)}</p>
                )}
              </div>
              <Link to={`/holds/${hold.holdId}`} className="button button-secondary">
                বিস্তারিত
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
