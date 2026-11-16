# ADR-0003: Where the frontend stores tokens

- **Status:** Accepted
- **Date:** 2026-11-12 (updated 2026-11-16: refresh token in an httpOnly cookie only, #24 and #27)
- **Deciders:** Owner A, Owner B

## Context

The Wallet API issues a short-lived JWT access token (15 minutes) and a refresh token (7 days, rotated on each use, with reuse detection). Both services accept the same access token. The browser needs it for REST calls (the `Authorization: Bearer` header) and for the SignalR connection to the Activity Service. Any script injected through an XSS bug can read `localStorage` and `sessionStorage` and send the contents anywhere. A stolen refresh token is worse than a stolen access token, because it keeps working for a week.

## Decision

**Access token: in memory only.** It lives in a module-level variable in `frontend/src/shared/auth/tokenStore.ts`. It is never written to `localStorage`, `sessionStorage`, IndexedDB, or a non-httpOnly cookie. Every API client and the SignalR connection read it through `tokenStore.get()`.

**Refresh token: in an httpOnly cookie** (built in #24; this was the stretch goal). On login and on every refresh the Wallet API sets `paylite_refresh` as `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`, valid for 7 days and rotated on every use:

- **HttpOnly:** JavaScript, and so an XSS bug, can never read it. The frontend code never sees the refresh token at all.
- **SameSite=Strict:** the browser never sends it on a request started by another site. That is the CSRF protection for `/api/auth/refresh`.
- **Path=/api/auth:** it only travels with login, refresh and logout, never with the other API calls or to the Activity Service.
- **Secure:** browsers treat `http://localhost` as secure, so this works in compose too (`Jwt:RefreshCookieSecure`).
- nginx serves the frontend and proxies `/api` from the same origin, so the cookie is first-party.

How the frontend uses it (`frontend/src/shared/auth/session.ts`, `AuthProvider.tsx`):

- **Page refresh (F5):** memory is empty, so on start-up the app calls `POST /api/auth/refresh` once; the browser adds the cookie and the response puts a new access token in memory. The user stays logged in.
- **Before the access token expires:** the app refreshes it one minute early, so a session no longer ends after 15 minutes.
- **Only one refresh in flight:** every refresh rotates the token, and the server treats a second request with an already-rotated token as theft (reuse detection) and revokes the whole family. Concurrent callers (e.g. React StrictMode running an effect twice) share one request.
- **Logout:** `POST /api/auth/logout` revokes the refresh token on the server and deletes the cookie; the in-memory access token is dropped at once.

**SignalR.** Browsers cannot set headers on a WebSocket, so the hub client sends the token as the `access_token` query parameter (`accessTokenFactory: () => tokenStore.get()`). The Activity Service accepts the query parameter only for paths starting with `/hubs/notifications`. Every other endpoint accepts the header only. Because tokens in URLs can end up in logs, neither nginx nor Serilog request logging records the query string for the hub path.

## Consequences

- An XSS bug can still *use* the access token while the page is open, but it can't steal a long-lived credential: the access token expires within 15 minutes and the refresh token is out of JavaScript's reach. Memory + httpOnly limits the damage; it does not replace preventing XSS. React escaping, no `dangerouslySetInnerHTML`, and a strict CSP from nginx are still required.
- A page refresh keeps you logged in, and an active session lasts up to the refresh token's 7 days instead of 15 minutes. The cost is one extra request on start-up and a short "restoring session" moment before protected pages render.
- Stealing the cookie still needs the browser itself (same origin, same device); a replayed old cookie is caught by reuse detection, which logs that user out everywhere.
- The refresh token exists **only** in the cookie: since #27, `/api/auth/login` and `/api/auth/refresh` return just `{ "accessToken": "..." }`, so not even a script running during login can read it.
- No token plumbing in components: one store (`tokenStore`) for the access token, one place (`session.ts`) for refresh and logout.
- The Activity Service is unchanged: it only ever sees the access token (header for REST, `?access_token=` on the hub path only), with the same claims.
- The hub-only query-string rule is covered by a test in the Activity Service: `NotificationsApiTests.TokenInQueryString_IsRejectedOutsideTheHub` (`?access_token=` on `/api/notifications` returns 401).
