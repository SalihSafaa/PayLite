// The access token lives ONLY in this module's memory: never localStorage/sessionStorage.
// Any script injected through an XSS bug can read localStorage, but it cannot reach a
// variable inside this module. A page refresh empties it; the session is then restored from the
// httpOnly refresh cookie (see session.ts). See docs/adr/0003-frontend-token-storage.md.

let accessToken: string | null = null
const listeners = new Set<() => void>()

export const tokenStore = {
  get: (): string | null => accessToken,
  set: (token: string | null): void => {
    accessToken = token
    listeners.forEach((listener) => listener())
  },
  subscribe: (listener: () => void): (() => void) => {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
}
