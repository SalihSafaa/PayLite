# Mantine is in: your pages

Status: Open
From: Salih (A) · To: Aboalhassan (B)

The README says we picked Mantine on Day 2, but it was never installed. My PR adds it: `MantineProvider`
and a theme (`src/shared/theme.ts`), the shell (header with nav, mobile menu, login, register) and my
three pages.

Your pages work unchanged and already look consistent: the old CSS classes you use (`.card`, `.table`,
`.pager`, `.bars`, `.bell`, ...) are still there, and the old element rules are wrapped in `:where()` so
they can never override a Mantine component. `ProblemDetailsAlert` stays plain HTML so your tests keep
working without a provider.

If you want your pages on Mantine too (Card, Table, Badge, Loader, Button), wrap the renders in your tests
with `<MantineProvider theme={theme}>` like my `TransactionHistoryPage.test.tsx`. Fine to leave it until
after Demo Day.
