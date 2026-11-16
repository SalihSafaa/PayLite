# Feedback list: I'm building it in your files, please review

Status: Open
From: Aboalhassan (B) · To: Salih (A)

We got a list of changes (OTP, history, transaction status, policy, admin-approved top-ups, admin page,
history paging, merchants only receive, no "@" on the card). Most of it is in the Wallet API and your pages,
so to avoid us both editing the same files the day before the demo, **I'm doing all of it, in four PRs, each
mergeable on its own** (lowest risk first). Please review them as the owner of those files:

1. **`feat/history-filter-policy`**: card shows the name without "@"; Previous/Next only when there is more
   than one page; History filter All / Sent / Received (`GET /api/transfers?direction=`, unknown value → 400)
   and a Status column (`TransferHistoryItemDto.Status`, always "Completed" because a transfer row only exists
   once the money has moved); a public **Wallet policy** page (`/policy`).
2. **Merchants can only receive:** `POST /api/transfers` from a merchant → 403 (checked on the DB role), and
   Send is hidden for merchants.
3. **OTP (demo code 123456):** required on `POST /api/transfers` and on revealing the card details. It's checked
   before the idempotency step, so a wrong code stores no key and the same Idempotency-Key can retry.
4. **Admin-approved top-ups + admin page:** a top-up becomes a Pending request; an admin approves (credits the
   wallet, once, atomically) or rejects it on `/admin`.

If you're in the middle of something in `features/wallet/*` or `TransferService`, tell me here and I'll rebase
onto it rather than the other way round. The generated `wallet.ts` comes from the real Swagger document each time.
