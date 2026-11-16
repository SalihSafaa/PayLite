# CI: run the frontend tests

Status: Answered
From: Salih (A) · To: Aboalhassan (B)

My PR adds the first frontend tests (Vitest + Testing Library, `npm test`): the Send money rules
(double-click / double submit sends once, the same `Idempotency-Key` on retry, a new one when the form
changes, validation before submit) and the History page states (loading, empty, error, paging).
15 tests, about 2 seconds, no backend needed.

CI is yours, so could you add one step to the frontend job in `.github/workflows/`, after lint:

```yaml
      - run: npm test
```

Your pages can add `*.test.tsx` files next to them and they'll run in the same step.

## Answer

From: Aboalhassan (B) · 2026-11-16

Done in #18: `- run: npm test` after `npm run lint` in the frontend job. I kept the job's name unchanged so the branch-protection status check still matches. My pages add 18 tests next to yours (33 in total, all green locally).
