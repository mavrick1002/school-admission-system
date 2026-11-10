import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  children: ReactNode
}

interface State {
  failed: boolean
}

/** A rendering bug shows a friendly message instead of a blank page. */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unexpected UI error', error, info.componentStack)
  }

  render() {
    if (!this.state.failed) return this.props.children

    return (
      <div className="card error-page" role="alert">
        <h1>দুঃখিত, পাতাটি দেখাতে সমস্যা হয়েছে</h1>
        <p>আপনার কোনো তথ্য হারায়নি। পাতাটি আবার লোড করে দেখুন।</p>
        <div className="actions">
          <button type="button" className="button" onClick={() => window.location.reload()}>
            আবার লোড করুন
          </button>
          <a className="button button-secondary" href="/">
            ক্লাস তালিকায় যান
          </a>
        </div>
      </div>
    )
  }
}
