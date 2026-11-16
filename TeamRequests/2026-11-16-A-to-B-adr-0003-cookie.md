# ADR-0003: the refresh-token cookie is in

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

I built ADR-0003's stretch goal in my PR: the refresh token is now an `HttpOnly; Secure; SameSite=Strict;
Path=/api/auth` cookie, so a page refresh no longer logs you out. The access token is still in memory only.

The ADR is yours: could you update its Consequences ("a refresh means logging in again, and a session
lasts at most 15 minutes") and the "Why keep the token in memory" answer in `docs/demo-script.md`?
Now: F5 keeps you logged in, the app refreshes the access token a minute before it expires, and logout
revokes the token on the server and deletes the cookie.

Nothing changes for your service: the access token and its claims are the same.

## Answer

From: Aboalhassan (B) · 2026-11-16

Nice, F5 keeping the session is a big improvement for the demo. Updated in `docs/adr-0003-refresh-cookie`:

- **ADR-0003:** the Decision now describes the cookie as built (name, every flag and why, start-up refresh,
  refresh a minute early, one refresh in flight because of reuse detection, logout revokes and deletes), and the
  Consequences no longer say "a refresh means logging in again".
- **`docs/demo-script.md`:** the "why memory, not localStorage" answer now covers the cookie, plus two new likely
  questions (CSRF, why one refresh at a time).
- **`docs/runbook.md`:** the two "log in again / a page refresh logs you out" rows are updated.

One question I wrote into the ADR's Consequences: `/api/auth/login` and `/api/auth/refresh` still return
`refreshToken` in the JSON body. The frontend ignores it, but a script running during login could read it. Do we
keep it for the tests and non-browser clients, or leave it out when the cookie is set? Fine either way for the demo.

Checked on my side: the SignalR connection reads the token through `accessTokenFactory` on every (re)connect, so it
picks up the refreshed access token automatically, and the bell only starts once the session is restored.

