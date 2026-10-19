import { renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { fakeApi, reply, session } from '../../test/fakeApi'
import { AuthProvider, useAuth } from './AuthContext'

// Refresh tokens rotate and the server ends every session when one is reused,
// so two refreshes racing with the same cookie would log the user out everywhere.
// reactStrictMode runs effects twice, as the app's <StrictMode> does in dev.
const renderAuth = () => renderHook(useAuth, { wrapper: AuthProvider, reactStrictMode: true })

describe('AuthProvider', () => {
  it('restores the session with a single refresh, even with StrictMode running effects twice', async () => {
    const api = fakeApi({ 'POST /api/auth/refresh': session('token-1') })

    const { result } = renderAuth()

    await waitFor(() => expect(result.current.status).toBe('authenticated'))
    expect(api.calls('POST /api/auth/refresh')).toHaveLength(1)
  })

  it('shares one refresh between requests that get a 401 at the same time', async () => {
    const api = fakeApi({
      'POST /api/auth/refresh': [session('token-1'), session('token-2')],
      'GET /api/tasks': [reply(401), reply(401), reply(200, {})],
    })
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('authenticated'))

    await Promise.all([result.current.api.request('/api/tasks'), result.current.api.request('/api/tasks')])

    expect(api.calls('POST /api/auth/refresh')).toHaveLength(2)
    expect(api.calls('GET /api/tasks').slice(2).map((c) => c.headers.get('Authorization'))).toEqual(['Bearer token-2', 'Bearer token-2'])
  })

  it('stays logged out when there is no session to restore', async () => {
    fakeApi({ 'POST /api/auth/refresh': reply(401) })

    const { result } = renderAuth()

    await waitFor(() => expect(result.current.status).toBe('anonymous'))
  })
})
