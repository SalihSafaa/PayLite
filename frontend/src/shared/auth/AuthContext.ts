import { createContext } from 'react'
import type { CurrentUser } from './jwt'

export interface AuthContextValue {
  user: CurrentUser | null
  /** Stores the access token in memory after a successful login. */
  signIn: (accessToken: string) => void
  signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
