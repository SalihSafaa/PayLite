import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MantineProvider } from '@mantine/core'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { theme } from '../../shared/theme'
import { SendMoneyPage } from './SendMoneyPage'

// Replace the real API client: these tests check what the page sends, not the server.
// GET is the wallet (for the balance preview), POST is the transfer under test.
vi.mock('../../shared/api/clients', () => ({ walletApi: { GET: vi.fn(), POST: vi.fn() } }))
const post = vi.mocked(walletApi.POST)
const get = vi.mocked(walletApi.GET)

type PostResult = Awaited<ReturnType<typeof walletApi.POST>>

function created(newBalance: number, replayed = false): PostResult {
    const headers = replayed ? { 'Idempotent-Replayed': 'true' } : undefined
    return {
        data: { transferId: 1, toUsername: 'bob', amount: 1000, currency: 'IQD', newBalance, createdAt: '2026-11-16T09:15:00Z', status: 'Completed' },
        error: undefined,
        response: new Response(null, { status: 201, headers }),
    } as PostResult
}

function failed(status: number, title: string): PostResult {
    return { data: undefined, error: { status, title }, response: new Response(null, { status }) } as PostResult
}

/** On the failure screen: "Try again" → the code → Confirm (the same transfer, the same key). */
async function retryWithCode(user: ReturnType<typeof userEvent.setup>) {
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    await typeCode(user)
    await user.click(screen.getByRole('button', { name: 'Confirm and send' }))
}

/** The options passed to the n-th POST (0-based). */
function optionsOfCall(n: number) {
    const call = post.mock.calls[n]
    if (!call) throw new Error(`POST was not called ${n + 1} times`)
    return call[1] as { body: unknown; params: { header: Record<string, string> } }
}

/** The Idempotency-Key header sent by the n-th POST (0-based). */
function keyOfCall(n: number): string | undefined {
    return optionsOfCall(n).params.header['Idempotency-Key']
}

function renderPage() {
    render(
        <MantineProvider theme={theme}>
            <MemoryRouter>
                <SendMoneyPage />
            </MemoryRouter>
        </MantineProvider>,
    )
    return userEvent.setup()
}

async function fillForm(user: ReturnType<typeof userEvent.setup>, toUsername: string, amount: string) {
    await user.clear(screen.getByLabelText('Recipient username'))
    await user.type(screen.getByLabelText('Recipient username'), toUsername)
    await user.clear(screen.getByLabelText('Amount (IQD)'))
    await user.type(screen.getByLabelText('Amount (IQD)'), amount)
}

/** Types the one-time code into the dialog's six boxes. */
async function typeCode(user: ReturnType<typeof userEvent.setup>, code = '123456') {
    await user.type(await screen.findByLabelText('Digit 1 of 6'), code)
}

/** Send → the code dialog → type the code → Confirm. What a user does to send money. */
async function sendWithCode(user: ReturnType<typeof userEvent.setup>, code = '123456') {
    await user.click(screen.getByRole('button', { name: 'Send' }))
    await typeCode(user, code)
    await user.click(screen.getByRole('button', { name: 'Confirm and send' }))
}

describe('SendMoneyPage', () => {
    beforeEach(() => {
        post.mockReset()
        get.mockReset()
        get.mockResolvedValue({
            data: { id: 1, balance: 100_000, currency: 'IQD', isMerchant: false },
            response: new Response(),
        } as Awaited<ReturnType<typeof walletApi.GET>>)
    })

    afterEach(() => {
        cleanup()
    })

    it.each([
        ['bo', '1000', 'Recipient username must be 3 to 50 characters.'],
        ['bob', '12.5', 'Amount must be a whole number of dinars.'],
        ['bob', '0', 'Amount must be at least 1 IQD.'],
        ['bob', '10000001', 'A single transfer cannot exceed'],
    ])('does not send an invalid form (%s, %s)', async (toUsername, amount, message) => {
        const user = renderPage()
        await fillForm(user, toUsername, amount)
        await user.click(screen.getByRole('button', { name: 'Send' }))

        expect(screen.getByRole('alert').textContent).toContain(message)
        expect(post).not.toHaveBeenCalled()
    })

    it('sends once on a double-click', async () => {
        // Keep the request pending so the second click happens while the first is still in flight.
        let finish: (result: PostResult) => void = () => {}
        post.mockReturnValueOnce(new Promise<PostResult>((resolve) => (finish = resolve)))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')

        await user.click(screen.getByRole('button', { name: 'Send' }))
        await typeCode(user)
        // The double-click that matters is on the button that actually sends.
        await user.dblClick(screen.getByRole('button', { name: 'Confirm and send' }))
        expect(screen.getByRole('button', { name: 'Sending…' }).hasAttribute('disabled')).toBe(true)
        finish(created(9000))

        expect(await screen.findByRole('status')).toBeTruthy()
        expect(post).toHaveBeenCalledTimes(1)
    })

    it('sends once when two submits arrive before React re-renders', async () => {
        // e.g. Enter pressed twice very fast: the button isn't disabled yet, only the ref guard stops it.
        post.mockResolvedValue(created(9000))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')
        await user.click(screen.getByRole('button', { name: 'Send' }))
        await typeCode(user)
        const form = screen.getByRole('button', { name: 'Confirm and send' }).closest('form')!

        act(() => {
            fireEvent.submit(form)
            fireEvent.submit(form)
        })

        await screen.findByRole('status')
        expect(post).toHaveBeenCalledTimes(1)
    })

    it('sends the amount as a number with an Idempotency-Key', async () => {
        post.mockResolvedValueOnce(created(9000))
        const user = renderPage()
        await fillForm(user, ' bob ', '1000')
        await sendWithCode(user)

        await screen.findByRole('status')
        expect(optionsOfCall(0).body).toEqual({ toUsername: 'bob', amount: 1000 })
        expect(keyOfCall(0)).toMatch(/^[0-9a-f-]{36}$/)
    })

    it('reuses the same key when the same form is retried after an error', async () => {
        post.mockResolvedValueOnce(failed(409, 'Insufficient funds.'))
        post.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        post.mockResolvedValueOnce(created(0))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')

        await sendWithCode(user)
        expect((await screen.findByRole('alert')).textContent).toContain('Insufficient funds.')
        await retryWithCode(user)
        expect((await screen.findByRole('alert')).textContent).toContain('Failed to fetch')
        await retryWithCode(user)
        await screen.findByRole('status')

        expect(post).toHaveBeenCalledTimes(3)
        expect(keyOfCall(1)).toBe(keyOfCall(0))
        expect(keyOfCall(2)).toBe(keyOfCall(0))
    })

    it('uses a new key when the form changes', async () => {
        post.mockResolvedValueOnce(failed(404, "No wallet found for 'bbo'."))
        post.mockResolvedValueOnce(created(9000))
        const user = renderPage()
        await fillForm(user, 'bbo', '1000')
        await sendWithCode(user)
        await screen.findByRole('alert')

        await user.click(screen.getByRole('button', { name: 'Edit transfer' }))
        await fillForm(user, 'bob', '1000')
        await sendWithCode(user)
        await screen.findByRole('status')

        expect(keyOfCall(1)).not.toBe(keyOfCall(0))
    })

    it('shows a receipt, and "Send another" starts a new transfer with a new key', async () => {
        post.mockResolvedValueOnce(created(9000))
        post.mockResolvedValueOnce(created(8000))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')
        await sendWithCode(user)

        const receipt = await screen.findByRole('status')
        expect(receipt.textContent).toContain('Money sent')
        expect(receipt.textContent).toContain('1,000')
        expect(receipt.textContent).toContain('bob')
        expect(receipt.textContent).toContain('#1')
        expect(receipt.textContent).toContain('Completed')
        expect(receipt.textContent).toContain('16 Nov 2026')
        expect(receipt.textContent).toContain('New balance')
        expect(receipt.textContent).toContain('9,000')

        await user.click(screen.getByRole('button', { name: 'Send another' }))
        expect(screen.getByLabelText<HTMLInputElement>('Amount (IQD)').value).toBe('')
        await fillForm(user, 'bob', '1000')
        await sendWithCode(user)
        await vi.waitFor(() => expect(screen.getByRole('status').textContent).toContain('8,000'))

        expect(keyOfCall(1)).not.toBe(keyOfCall(0))
    })

    it('shows a failure screen with the reason and what was attempted, and says no money moved', async () => {
        post.mockResolvedValueOnce(failed(409, 'Insufficient funds.'))
        const user = renderPage()
        await fillForm(user, 'bob', '25000')
        await sendWithCode(user)

        const failure = await screen.findByRole('alert')
        expect(failure.textContent).toContain('Transfer failed')
        expect(failure.textContent).toContain('Insufficient funds.')
        expect(failure.textContent).toContain('bob')
        expect(failure.textContent).toContain('25,000')
        expect(failure.textContent).toContain('No money left your wallet.')
    })

    it("says it can't confirm (not failed) when the server can't be reached", async () => {
        post.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')
        await sendWithCode(user)

        const failure = await screen.findByRole('alert')
        expect(failure.textContent).toContain("We couldn't confirm the transfer")
        expect(failure.textContent).toContain('never sent twice')
    })

    it('says so when the server replays an earlier transfer', async () => {
        post.mockResolvedValueOnce(created(9000, true))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')
        await sendWithCode(user)

        expect((await screen.findByRole('status')).textContent).toContain('nothing was sent twice')
    })

    it('asks for the code before sending, and sends it with the request', async () => {
        post.mockResolvedValueOnce(created(9000))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')

        await user.click(screen.getByRole('button', { name: 'Send' }))
        expect(await screen.findByText(/Enter the 6-digit code to send/)).toBeTruthy()
        expect(post).not.toHaveBeenCalled()
        // Confirm stays disabled until all six digits are there.
        expect(screen.getByRole('button', { name: 'Confirm and send' }).hasAttribute('disabled')).toBe(true)

        await typeCode(user)
        await user.click(screen.getByRole('button', { name: 'Confirm and send' }))

        await screen.findByRole('status')
        expect(optionsOfCall(0).params.header['X-OTP-Code']).toBe('123456')
    })

    it('keeps the dialog open on a wrong code and retries with the same key', async () => {
        post.mockResolvedValueOnce({
            data: undefined,
            error: { status: 403, type: 'urn:paylite:otp-invalid', title: 'The one-time code is wrong.' },
            response: new Response(null, { status: 403 }),
        } as PostResult)
        post.mockResolvedValueOnce(created(9000))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')

        await sendWithCode(user, '654321')
        expect((await screen.findByRole('alert')).textContent).toContain('The one-time code is wrong.')
        expect(screen.getByRole('button', { name: 'Confirm and send' })).toBeTruthy() // still in the dialog

        await typeCode(user, '123456')
        await user.click(screen.getByRole('button', { name: 'Confirm and send' }))

        await screen.findByRole('status')
        expect(post).toHaveBeenCalledTimes(2)
        expect(keyOfCall(1)).toBe(keyOfCall(0))
    })

    it('sends nothing when the dialog is closed', async () => {
        const user = renderPage()
        await fillForm(user, 'bob', '1000')

        await user.click(screen.getByRole('button', { name: 'Send' }))
        await screen.findByLabelText('Digit 1 of 6')
        await user.keyboard('{Escape}')

        expect(post).not.toHaveBeenCalled()
    })
})
