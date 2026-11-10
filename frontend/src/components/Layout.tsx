import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { ErrorBoundary } from './ErrorBoundary'
import { ServerBanner } from './ServerBanner'
import { StudentIdField } from './StudentIdField'

export function Layout() {
  const location = useLocation()

  return (
    <>
      <header className="site-header">
        <div className="container header-inner">
          <NavLink to="/" className="brand">
            স্কুল ভর্তি
          </NavLink>
          <nav className="nav" aria-label="প্রধান মেনু">
            <NavLink to="/" end>
              ক্লাস তালিকা
            </NavLink>
            <NavLink to="/me">আমার আবেদন</NavLink>
            <NavLink to="/admin/classes/new">নতুন ক্লাস (অ্যাডমিন)</NavLink>
          </nav>
          <StudentIdField />
        </div>
      </header>
      <ServerBanner />
      <main className="container">
        {/* Keyed by path, so a crash on one screen does not stick after navigating away. */}
        <ErrorBoundary key={location.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </>
  )
}
