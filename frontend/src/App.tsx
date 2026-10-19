import { AuthPage } from './features/auth/AuthPage'
import { useAuth } from './features/auth/AuthContext'
import { TasksPage } from './features/tasks/TasksPage'

/** Two screens, chosen by the session: no router needed. */
export function App() {
  const { status } = useAuth()

  if (status === 'checking')
    return (
      <main className="centered" aria-busy="true">
        <p className="muted">Loading…</p>
      </main>
    )

  return status === 'authenticated' ? <TasksPage /> : <AuthPage />
}
