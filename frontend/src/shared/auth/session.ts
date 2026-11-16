import { walletApi } from '../api/clients'

// The refresh token lives in an httpOnly cookie (ADR-0003): this code never sees it. The browser sends it
// by itself, but only to /api/auth/... on our own origin (Path=/api/auth, SameSite=Strict).
//
// Only ONE refresh may be in flight: each refresh rotates the token, and the server treats a second request
// with the already-rotated token as a stolen token and logs the user out everywhere. React's StrictMode runs
// effects twice in development, so two callers at the same moment must share the same request.
let inFlight: Promise<string | null> | null = null

/** Exchanges the refresh cookie for a new access token. Resolves null when there is no valid session. */
export function refreshSession(): Promise<string | null> {
  inFlight ??= walletApi
    .POST('/api/auth/refresh', {})
    .then(({ data }) => data?.accessToken ?? null)
    .catch(() => null)
    .finally(() => {
      inFlight = null
    })
  return inFlight
}

/** Revokes the refresh token on the server and deletes the cookie. Best effort: the UI logs out anyway. */
export function endSession(accessToken: string): void {
  walletApi
    .POST('/api/auth/logout', { headers: { Authorization: `Bearer ${accessToken}` } })
    .catch(() => {
      // Nothing to do: the access token is already gone from memory and expires within 15 minutes.
    })
}
