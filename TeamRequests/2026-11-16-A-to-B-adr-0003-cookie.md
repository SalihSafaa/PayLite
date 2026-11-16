# ADR-0003: the refresh-token cookie is in

Status: Open
From: Salih (A) · To: Aboalhassan (B)

I built ADR-0003's stretch goal in my PR: the refresh token is now an `HttpOnly; Secure; SameSite=Strict;
Path=/api/auth` cookie, so a page refresh no longer logs you out. The access token is still in memory only.

The ADR is yours: could you update its Consequences ("a refresh means logging in again, and a session
lasts at most 15 minutes") and the "Why keep the token in memory" answer in `docs/demo-script.md`?
Now: F5 keeps you logged in, the app refreshes the access token a minute before it expires, and logout
revokes the token on the server and deletes the cookie.

Nothing changes for your service: the access token and its claims are the same.
