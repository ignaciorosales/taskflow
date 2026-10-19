import { useId, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { useAuth } from './AuthContext'

const DEMO = { email: 'demo@taskflow.dev', password: 'Demo123!' }

export function AuthPage() {
  const { login, register } = useAuth()
  const id = useId()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function switchTo(next: 'login' | 'register') {
    setMode(next)
    setFieldErrors({})
    setFormError(null)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setFieldErrors({})
    setFormError(null)
    try {
      await (mode === 'login' ? login : register)(email, password)
    } catch (error) {
      if (!(error instanceof ApiError)) setFormError("Couldn't reach the server. Please try again.")
      else if (Object.keys(error.fieldErrors).length > 0) setFieldErrors(error.fieldErrors)
      else setFormError(error.message)
      setBusy(false)
    }
  }

  const describe = (field: string) =>
    fieldErrors[field] ? { 'aria-invalid': true, 'aria-describedby': `${id}-${field}-error` } : {}

  return (
    <main className="auth">
      <section className="card auth-card">
        <h1 className="brand">TaskFlow</h1>
        <p className="muted">What's pending, what's in progress and what's overdue, at a glance.</p>

        <div className="tabs" role="tablist" aria-label="Account">
          {(['login', 'register'] as const).map((tab) => (
            <button
              key={tab}
              role="tab"
              type="button"
              aria-selected={mode === tab}
              className={mode === tab ? 'tab active' : 'tab'}
              onClick={() => switchTo(tab)}
            >
              {tab === 'login' ? 'Log in' : 'Create account'}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="field">
            <label htmlFor={`${id}-email`}>Email</label>
            <input
              id={`${id}-email`}
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              {...describe('email')}
            />
            {fieldErrors.email && (
              <p className="field-error" id={`${id}-email-error`}>
                {fieldErrors.email.join(' ')}
              </p>
            )}
          </div>

          <div className="field">
            <label htmlFor={`${id}-password`}>Password</label>
            <input
              id={`${id}-password`}
              type="password"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              {...describe('password')}
            />
            {fieldErrors.password ? (
              <p className="field-error" id={`${id}-password-error`}>
                {fieldErrors.password.join(' ')}
              </p>
            ) : (
              mode === 'register' && <p className="hint">At least 8 characters, with a letter, a digit and a symbol.</p>
            )}
          </div>

          {formError && (
            <p className="form-error" role="alert">
              {formError}
            </p>
          )}

          <button type="submit" className="button primary wide" disabled={busy}>
            {busy ? 'Please wait…' : mode === 'login' ? 'Log in' : 'Create account'}
          </button>
        </form>

        <aside className="demo">
          <p>
            <strong>Just looking?</strong> Use the demo account: <code>{DEMO.email}</code> / <code>{DEMO.password}</code>
          </p>
          <button
            type="button"
            className="button secondary"
            onClick={() => {
              switchTo('login')
              setEmail(DEMO.email)
              setPassword(DEMO.password)
            }}
          >
            Fill in demo account
          </button>
        </aside>
      </section>
    </main>
  )
}
