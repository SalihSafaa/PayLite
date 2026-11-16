import { defineConfig } from '@playwright/test'

// End-to-end tests against the real stack: start it first with `docker compose up --build`,
// then `npm run e2e`. Not part of CI (they need every container running).
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    trace: 'off',
  },
})
