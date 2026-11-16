import { useCallback, useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { decodeUser } from './jwt'
import { endSession, refreshSession } from './session'
import { tokenStore } from './tokenStore'

// Refresh this long before the access token (15 min) expires.
const REFRESH_MARGIN_MS = 60_000

export function AuthProvider({ children }: { children: ReactNode }) {
  const token = useSyncExternalStore(tokenStore.subscribe, tokenStore.get)
  const [restoring, setRestoring] = useState(() => tokenStore.get() === null)

  // After a page refresh the memory is empty: ask the server for a new access token using the cookie.
  useEffect(() => {
    if (!restoring) return
    let ignore = false
    refreshSession()
      .then((accessToken) => {
        if (accessToken) tokenStore.set(accessToken)
      })
      .finally(() => {
        if (!ignore) setRestoring(false)
      })
    return () => {
      ignore = true
    }
  }, [restoring])

  // Keep the session alive: get a new access token a minute before this one expires.
  // If the refresh fails (expired, revoked), the token becomes null and the user is sent to the login page.
  useEffect(() => {
    if (!token) return
    const wait = Math.max(decodeUser(token).expiresAt.getTime() - Date.now() - REFRESH_MARGIN_MS, 5_000)
    const timer = setTimeout(() => {
      void refreshSession().then((accessToken) => tokenStore.set(accessToken))
    }, wait)
    return () => clearTimeout(timer)
  }, [token])

  const signIn = useCallback((accessToken: string) => tokenStore.set(accessToken), [])

  const signOut = useCallback(() => {
    const accessToken = tokenStore.get()
    tokenStore.set(null)
    if (accessToken) endSession(accessToken)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ user: token ? decodeUser(token) : null, restoring, signIn, signOut }),
    [token, restoring, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
