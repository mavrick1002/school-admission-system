import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { ApiError } from './api/client'
import App from './App.tsx'
import { ErrorBoundary } from './components/ErrorBoundary'
import './index.css'
import { StudentIdProvider } from './student/StudentIdProvider'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // A 4xx answer will not change on retry; an outage or a 5xx might.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
    mutations: {
      // Never repeat a POST or DELETE on its own.
      retry: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <StudentIdProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </StudentIdProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
)
