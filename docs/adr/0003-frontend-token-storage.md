# ADR-0003: Where the frontend stores tokens

- **Status:** Proposed
- **Date:** 2026-11-12
- **Deciders:** Owner A, Owner B

## Context

The Wallet API issues a short-lived JWT access token (15 minutes) and a refresh token (7 days, rotated on each use, with reuse detection). Both services accept the same access token. The browser needs it for REST calls (the `Authorization: Bearer` header) and for the SignalR connection to the Activity Service. Any script injected through an XSS bug can read `localStorage` and `sessionStorage` and send the contents anywhere. A stolen refresh token is worse than a stolen access token, because it keeps working for a week.

## Decision

**Access token: in memory only.** It lives in a module-level variable in `frontend/src/shared/auth/tokenStore.ts`. It is never written to `localStorage`, `sessionStorage`, IndexedDB, or a non-httpOnly cookie. Every API client and the SignalR connection read it through `tokenStore.get()`.

**Refresh token: not kept in the browser for now.** The frontend ignores the refresh token in the login response. A page refresh, or the access token expiring after 15 minutes, sends the user back to the login page. The brief accepts this. Logout clears the in-memory token and calls `POST /api/auth/logout` while we still have the refresh token in hand, so it is revoked on the server.

**Stretch goal (if time allows): refresh token in an httpOnly cookie.** The Wallet API would set it as `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`. Because nginx serves the frontend and proxies `/api` from the same origin, the cookie is first-party and JavaScript can never read it. When the page loads, the app calls `/api/auth/refresh` to get a new access token into memory. That brings in CSRF on `/api/auth/refresh`, but `SameSite=Strict` together with a JSON-only body is enough protection here.

**SignalR.** Browsers cannot set headers on a WebSocket, so the hub client sends the token as the `access_token` query parameter (`accessTokenFactory: () => tokenStore.get()`). The Activity Service accepts the query parameter only for paths starting with `/hubs/notifications`. Every other endpoint accepts the header only. Because tokens in URLs can end up in logs, neither nginx nor Serilog request logging records the query string for the hub path.

## Consequences

- An XSS bug can still use the token while the page is open, but it cannot steal a long-lived credential to use later. Memory only limits the damage; it does not replace preventing XSS. React escaping, no `dangerouslySetInnerHTML`, and a strict CSP from nginx are still required.
- Worse UX: a refresh means logging in again, and a session lasts at most 15 minutes. It is acceptable for the demo, and the cookie stretch goal fixes both.
- No token plumbing in components: one store, one place to change.
- The hub-only query-string rule must be covered by a test in the Activity Service: `?access_token=` on `/api/notifications` returns 401.
