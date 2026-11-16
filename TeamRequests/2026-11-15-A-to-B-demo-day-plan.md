# Demo Day (Tue 2026-11-17): what's left and who does what

Status: Open
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
