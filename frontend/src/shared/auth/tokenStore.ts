// The access token lives ONLY in this module's memory: never localStorage/sessionStorage.
// Any script injected through an XSS bug can read localStorage, but it cannot reach a
// variable inside this module. Trade-off (accepted in the brief): a page refresh logs you out.
// See docs/adr/0003-frontend-token-storage.md.

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
