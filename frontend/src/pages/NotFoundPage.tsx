import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <section className="narrow card empty">
      <h1>পাতাটি পাওয়া যায়নি</h1>
      <p>লিংকটি হয়তো ভুল।</p>
      <Link to="/" className="button">
        ক্লাস তালিকায় যান
      </Link>
    </section>
  )
}
