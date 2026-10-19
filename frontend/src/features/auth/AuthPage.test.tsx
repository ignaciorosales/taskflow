import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { App } from '../../App'
import { board, fakeApi, reply, session } from '../../test/fakeApi'
import { AuthProvider } from './AuthContext'

// No session to restore, so the app starts on the login screen.
function renderApp(routes: Parameters<typeof fakeApi>[0] = {}) {
  const api = fakeApi({ 'POST /api/auth/refresh': reply(401), 'GET /api/tasks': board([]), ...routes })
  render(
    <AuthProvider>
      <App />
    </AuthProvider>,
  )
  return { api, user: userEvent.setup() }
}

describe('AuthPage', () => {
  it('logs in with what I typed and takes me to my board', async () => {
    const { api, user } = renderApp({ 'POST /api/auth/login': session('token-1') })

    await user.type(await screen.findByLabelText('Email'), 'me@example.com')
    await user.type(screen.getByLabelText('Password'), 'Secret1!')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { name: 'My tasks' })).toBeInTheDocument()
    expect(api.calls('POST /api/auth/login')[0].body).toEqual({ email: 'me@example.com', password: 'Secret1!' })
  })

  it('shows the server message when the credentials are wrong, and stays on the login screen', async () => {
    const { user } = renderApp({ 'POST /api/auth/login': reply(401, { title: 'Email or password is incorrect.' }) })

    await user.type(await screen.findByLabelText('Email'), 'me@example.com')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
    expect(screen.queryByRole('heading', { name: 'My tasks' })).not.toBeInTheDocument()
  })

  it('registers through the Create account tab and shows the password rules next to the field', async () => {
    const { api, user } = renderApp({
      'POST /api/auth/register': reply(400, {
        title: 'One or more validation errors occurred.',
        errors: { password: ['Password must contain a digit.'] },
      }),
    })

    await user.click(await screen.findByRole('tab', { name: 'Create account' }))
    await user.type(screen.getByLabelText('Email'), 'me@example.com')
    await user.type(screen.getByLabelText('Password'), 'NoDigits!')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Password must contain a digit.')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Password must contain a digit.')
    expect(api.calls('POST /api/auth/register')).toHaveLength(1)
  })

  it('fills in the demo account shown on the screen', async () => {
    const { user } = renderApp()

    await user.click(await screen.findByRole('button', { name: 'Fill in demo account' }))

    expect(screen.getByLabelText('Email')).toHaveValue('demo@taskflow.dev')
    expect(screen.getByLabelText('Password')).toHaveValue('Demo123!')
    expect(screen.getByText('demo@taskflow.dev')).toBeInTheDocument()
  })
})
