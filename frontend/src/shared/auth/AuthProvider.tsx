import { useCallback, useMemo, useSyncExternalStore, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { decodeUser } from './jwt'
import { tokenStore } from './tokenStore'

export function AuthProvider({ children }: { children: ReactNode }) {
  const token = useSyncExternalStore(tokenStore.subscribe, tokenStore.get)

  const signIn = useCallback((accessToken: string) => tokenStore.set(accessToken), [])
  // TODO (shared): also call POST /api/auth/logout so the refresh token is revoked server-side.
  const signOut = useCallback(() => tokenStore.set(null), [])

  const value = useMemo<AuthContextValue>(
    () => ({ user: token ? decodeUser(token) : null, signIn, signOut }),
    [token, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
