import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MantineProvider } from '@mantine/core'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { theme } from '../../shared/theme'
import { SendMoneyPage } from './SendMoneyPage'

// Replace the real API client: these tests check what the page sends, not the server.
vi.mock('../../shared/api/clients', () => ({ walletApi: { POST: vi.fn() } }))
const post = vi.mocked(walletApi.POST)

type PostResult = Awaited<ReturnType<typeof walletApi.POST>>

function created(newBalance: number, replayed = false): PostResult {
    const headers = replayed ? { 'Idempotent-Replayed': 'true' } : undefined
    return {
        data: { transferId: 1, toUsername: 'bob', amount: 1000, currency: 'IQD', newBalance },
        error: undefined,
        response: new Response(null, { status: 201, headers }),
    } as PostResult
}

function failed(status: number, title: string): PostResult {
    return { data: undefined, error: { status, title }, response: new Response(null, { status }) } as PostResult
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

describe('SendMoneyPage', () => {
    beforeEach(() => {
        post.mockReset()
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

        await user.dblClick(screen.getByRole('button', { name: 'Send' }))
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
        const form = screen.getByRole('button', { name: 'Send' }).closest('form')!

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
        await user.click(screen.getByRole('button', { name: 'Send' }))

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

        await user.click(screen.getByRole('button', { name: 'Send' }))
        expect((await screen.findByRole('alert')).textContent).toContain('Insufficient funds.')
        await user.click(screen.getByRole('button', { name: 'Send' }))
        expect((await screen.findByRole('alert')).textContent).toContain('Failed to fetch')
        await user.click(screen.getByRole('button', { name: 'Send' }))
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
        await user.click(screen.getByRole('button', { name: 'Send' }))
        await screen.findByRole('alert')

        await fillForm(user, 'bob', '1000')
        await user.click(screen.getByRole('button', { name: 'Send' }))
        await screen.findByRole('status')

        expect(keyOfCall(1)).not.toBe(keyOfCall(0))
    })

    it('shows the new balance and uses a new key for the next transfer', async () => {
        post.mockResolvedValueOnce(created(9000))
        post.mockResolvedValueOnce(created(8000))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')
        await user.click(screen.getByRole('button', { name: 'Send' }))

        const status = await screen.findByRole('status')
        expect(status.textContent).toContain('New balance:')
        expect(status.textContent).toContain('9,000')
        expect(screen.getByLabelText<HTMLInputElement>('Amount (IQD)').value).toBe('')

        await fillForm(user, 'bob', '1000')
        await user.click(screen.getByRole('button', { name: 'Send' }))
        await screen.findByText(/8,000/)

        expect(keyOfCall(1)).not.toBe(keyOfCall(0))
    })

    it('says so when the server replays an earlier transfer', async () => {
        post.mockResolvedValueOnce(created(9000, true))
        const user = renderPage()
        await fillForm(user, 'bob', '1000')
        await user.click(screen.getByRole('button', { name: 'Send' }))

        expect((await screen.findByRole('status')).textContent).toContain('nothing was sent twice')
    })
})
