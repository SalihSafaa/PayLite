import { expect, test, type APIRequestContext, type Page } from '@playwright/test'

// The brief's happy path, end to end through nginx, both services, SQL Server and Kafka:
// a customer sends money to a merchant with a double-click; exactly one transfer is made,
// and the merchant's bell and daily summary update live, without a page refresh.

const PASSWORD = 'Passw0rd!123'

async function register(request: APIRequestContext, username: string, accountType: 'User' | 'Merchant') {
  const response = await request.post('/api/auth/register', { data: { username, password: PASSWORD, accountType } })
  expect(response.status()).toBe(201)
}

async function topUp(request: APIRequestContext, username: string, amount: number) {
  const login = await request.post('/api/auth/login', { data: { username, password: PASSWORD } })
  const { accessToken } = (await login.json()) as { accessToken: string }
  const response = await request.post('/api/wallets/me/top-ups', {
    data: { amount },
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  expect(response.ok()).toBe(true)
}

// The token is kept in memory only, so the test logs in through the UI and then uses the nav links
// (a full page load would log it out, exactly like a real user pressing F5).
async function logIn(page: Page, username: string) {
  await page.goto('/login')
  await page.getByLabel('Username', { exact: true }).fill(username)
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
  await page.getByRole('button', { name: /log in|sign in/i }).click()
  await expect(page.getByText('Balance')).toBeVisible()
}

test('a double-clicked transfer is sent once and the merchant sees it live', async ({ browser, request }) => {
  const id = Date.now().toString().slice(-7)
  const customer = `e2e_cust_${id}`
  const merchant = `e2e_shop_${id}`
  await register(request, customer, 'User')
  await register(request, merchant, 'Merchant')
  await topUp(request, customer, 100_000)

  // Right window: the merchant waits on the daily summary.
  const merchantPage = await (await browser.newContext()).newPage()
  await logIn(merchantPage, merchant)
  await merchantPage.getByRole('navigation').getByRole('link', { name: 'Daily summary' }).click()
  await expect(merchantPage.getByRole('heading', { name: 'Daily summary' })).toBeVisible()
  const bell = merchantPage.locator('a.bell')
  await expect(bell).toHaveAttribute('aria-label', 'Notifications')

  // Left window: the customer sends 2,500 with a double-click.
  const customerPage = await (await browser.newContext()).newPage()
  const transferPosts: string[] = []
  customerPage.on('request', (r) => {
    if (r.method() === 'POST' && new URL(r.url()).pathname === '/api/transfers') {
      transferPosts.push(r.headers()['idempotency-key'] ?? '')
    }
  })
  await logIn(customerPage, customer)
  await customerPage.getByRole('navigation').getByRole('link', { name: 'Send money' }).click()
  await customerPage.getByLabel('Recipient username').fill(merchant)
  await customerPage.getByLabel('Amount (IQD)').fill('2500')
  await customerPage.getByRole('button', { name: 'Send' }).dblclick()

  const status = customerPage.getByRole('status')
  await expect(status).toContainText(`Sent IQD 2,500 to ${merchant}.`)
  await expect(status).toContainText('New balance: IQD 97,500')
  expect(transferPosts).toHaveLength(1)

  // The merchant's page was never reloaded: the bell and the summary update over SignalR.
  await expect(bell).toHaveAttribute('aria-label', 'Notifications, 1 unread', { timeout: 15_000 })
  await expect(merchantPage.getByLabel('Payment count')).toHaveText('1', { timeout: 15_000 })
  await expect(merchantPage.getByLabel('Total received')).toHaveText('IQD 2,500')

  // And the customer's history shows exactly one sent transfer.
  await customerPage.getByRole('navigation').getByRole('link', { name: 'History' }).click()
  const rows = customerPage.locator('tbody tr')
  await expect(rows).toHaveCount(1)
  await expect(rows.first()).toContainText(merchant)
  await expect(rows.first()).toContainText('Sent')
})
