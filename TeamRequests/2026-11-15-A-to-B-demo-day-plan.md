# Demo Day (Tue 2026-11-17): what's left and who does what

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

My side is done and on `main` (all endpoints, Send money / History / dashboard, scenarios 1-4
tested, scenario 4 drilled in compose). On `main` your side still has placeholders, so let's agree
on the plan:

1. **Still to come from you?** The notifications endpoints, the SignalR hub (`/hubs/notifications`,
   token via `access_token`), the merchant daily summary endpoint, and your pages (Notifications,
   Merchant summary, the bell). Which of these will be ready by Tuesday? If something won't make it,
   let's decide now what we cut. Polish goes first, never a failure scenario.
2. **Merchant "Incoming payments" page.** It's in your folder (`features/activity/MerchantPaymentsPage.tsx`)
   but it only calls my endpoint (`GET /api/merchants/me/payments`). Shall I build it, so you can
   focus on scenario 7 and the notifications? I'd reuse the History page's table and paging.
3. **Demo script** (yours): my suggestion for the order:
   register a user and a merchant → top up → send money (double-click on purpose) → history →
   scenario 4 (stop Kafka live) → scenario 6 → scenario 7 → notifications/bell.
   I'll present 1-4 and the Wallet parts; you present 5-7 and the Activity parts.
4. **Rehearsal:** can we do one full run in compose tomorrow (Monday)? Every drill command we need is
   in the runbook requests I sent.

Please answer under `## Answer`.

## Answer

From: Aboalhassan (B) · 2026-11-16

1. **Everything from your list is ready, nothing cut:**
   - #16: dead-letter topic + retry limit, quieter logs, scenario 6 and 7 tests
   - #17: `GET /api/notifications`, `POST /api/notifications/{id}/read`, `GET /api/merchants/me/daily-summary`, the SignalR hub `/hubs/notifications` (token via `access_token` on the hub path only), live push after commit
   - #18: the bell (live unread count), Notifications, Daily summary (hourly bars, updates live), Incoming payments
   - #19 (this one): README scenario table, runbook drills, demo script

   They're stacked: please review and merge **#16 → #17 → #18**, then #19.
2. **Incoming payments:** thanks for offering. I built it in #18 on your `GET /api/merchants/me/payments`, reusing your History pattern (paged table, same states), and it reloads page 1 on a live payment. Please check it in your review.
3. **Demo script:** agreed, written in `docs/demo-script.md` with your order: happy path, then scenarios 1, 4, 6, 5, 7. You present 1–4 and the Wallet parts; I present 5–7 and the Activity parts. I added drill 5 (re-publishing an already-processed event) because the brief requires 1, 4 and 5 live.
4. **Rehearsal:** yes, today (Monday) once #16–#18 are merged: `docker compose down -v && docker compose up --build`, then the script end to end. The commands are in `docs/runbook.md`.
