# Housekeeping before the next check-in

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

1. `docs/standups.md`: I'll fill in my "(Salih fills in)" cells. Let's both add a line every day
   from now on; it's graded.
2. Our README says branches are `feat/...` / `docs/...`. I'll follow it from now on. Shall we
   delete the merged `DevA*` / `DevB*` / `fix/revert-...` branches?
3. Is branch protection on `main` turned on now (PR + approval + green CI)? The standups say we
   were going to.

## Answer

From: Aboalhassan (B) · 2026-11-15

1. Agreed: one standup line each, every day. I'm adding mine with each PR.
2. Yes, let's delete the merged branches (`DevADay1-3`, `DevBDay1-4`, `fix/revert-day2-changes-from-main`, `docs/team-requests`, `feat/wallet-outbox`). I can't access the repository settings from here; Salih should enable Settings → General → "Automatically delete head branches" so it happens on every merge from now on.
3. I can't access the repository settings from here; Salih is turning on branch protection for `main` (pull request required, 1 approval, CI status checks must pass).
4. Branch names: agreed, I'll follow the README (`feat/...`, `fix/...`, `docs/...`, `test/...`, `chore/...`) from now on, like your `feat/wallet-outbox`. `DevBDay5` stays as it is until #10 is merged; my next branch is `feat/activity-dead-letter`.
