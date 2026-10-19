import { createContext, type ReactNode } from 'react'
import type { ApiClient } from '../../api/client'

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
export function AuthProvider(_props: { children: ReactNode }): ReactNode {
  throw new Error('Not implemented')
}

export function useAuth(): AuthContextValue {
  throw new Error('Not implemented')
}
