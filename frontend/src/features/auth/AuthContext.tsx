import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { createApiClient, send, toApiError, type ApiClient } from '../../api/client'

export type AuthStatus = 'checking' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  api: ApiClient
  login(email: string, password: string): Promise<void>
  register(email: string, password: string): Promise<void>
  logout(): Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

/** Keeps the access token in memory only; the refresh token lives in an HttpOnly cookie. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('checking')
  const accessToken = useRef<string | null>(null)
  const refreshing = useRef<Promise<string | null> | null>(null)

  const setSession = useCallback((token: string | null) => {
    accessToken.current = token
    setStatus(token ? 'authenticated' : 'anonymous')
  }, [])

  // Refresh tokens rotate and a reused one ends every session, so concurrent callers
  // (several 401s at once, StrictMode's double effects) must share a single refresh.
  const refresh = useCallback(() => {
    refreshing.current ??= (async () => {
      try {
        const response = await send('/api/auth/refresh', { method: 'POST' })
        accessToken.current = response.ok ? ((await response.json()) as { accessToken: string }).accessToken : null
        return accessToken.current
      } catch {
        return null
      } finally {
        refreshing.current = null
      }
    })()
    return refreshing.current
  }, [])

  const api = useMemo(
    () => createApiClient({ getAccessToken: () => accessToken.current, refresh, onUnauthorized: () => setSession(null) }),
    [refresh, setSession],
  )

  // A reload loses the in-memory token; the refresh cookie brings the session back.
  useEffect(() => {
    let active = true
    refresh().then((token) => active && setSession(token))
    return () => {
      active = false
    }
  }, [refresh, setSession])

  const value = useMemo<AuthContextValue>(() => {
    const startSession = async (path: string, email: string, password: string) => {
      const response = await send(path, { method: 'POST', json: { email, password } })
      if (!response.ok) throw await toApiError(response)
      setSession(((await response.json()) as { accessToken: string }).accessToken)
    }
    return {
      status,
      api,
      login: (email, password) => startSession('/api/auth/login', email, password),
      register: (email, password) => startSession('/api/auth/register', email, password),
      logout: async () => {
        await send('/api/auth/logout', { method: 'POST' }).catch(() => undefined)
        setSession(null)
      },
    }
  }, [status, api, setSession])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.')
  return context
}
