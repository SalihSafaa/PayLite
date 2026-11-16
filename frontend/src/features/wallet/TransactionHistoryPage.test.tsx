import { cleanup, render, screen } from '@testing-library/react'
import { MantineProvider } from '@mantine/core'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { walletApi } from '../../shared/api/clients'
import { theme } from '../../shared/theme'
import { TransactionHistoryPage } from './TransactionHistoryPage'
import type { TransferHistoryItem } from './useTransferHistory'

vi.mock('../../shared/api/clients', () => ({ walletApi: { GET: vi.fn() } }))
const get = vi.mocked(walletApi.GET)

type GetResult = Awaited<ReturnType<typeof walletApi.GET>>

function item(transferId: number, direction: 'Sent' | 'Received'): TransferHistoryItem {
    return {
        transferId,
        direction,
        counterpartyUsername: direction === 'Sent' ? 'bob' : 'carol',
        amount: 1000,
        currency: 'IQD',
        createdAt: '2026-11-15T10:00:00Z',
        balanceAfter: direction === 'Sent' ? 9000 : null,
        status: 'Completed',
    }
}

function page(items: TransferHistoryItem[], totalCount: number, pageNumber = 1): GetResult {
    return {
        data: { items, totalCount, page: pageNumber, pageSize: 10 },
        error: undefined,
        response: new Response(null, { status: 200 }),
    } as GetResult
}

function renderPage() {
    render(
        <MantineProvider theme={theme}>
            <MemoryRouter>
                <TransactionHistoryPage />
            </MemoryRouter>
        </MantineProvider>,
    )
    return userEvent.setup()
}

describe('TransactionHistoryPage', () => {
    beforeEach(() => {
        get.mockReset()
    })

    afterEach(() => {
        cleanup()
    })

    it('shows a loading state, then the empty state', async () => {
        get.mockResolvedValueOnce(page([], 0))
        renderPage()

        expect(screen.getByText('Loading transactions…')).toBeTruthy()
        expect(await screen.findByText(/No transactions yet/)).toBeTruthy()
    })

    it('shows sent and received rows', async () => {
        get.mockResolvedValueOnce(page([item(2, 'Received'), item(1, 'Sent')], 2))
        renderPage()

        const rows = await screen.findAllByRole('row')
        expect(rows).toHaveLength(3) // header + 2
        expect(rows[1]?.textContent).toContain('Received')
        expect(rows[1]?.textContent).toContain('carol')
        expect(rows[2]?.textContent).toContain('Sent')
        expect(rows[2]?.textContent).toContain('9,000')
        expect(screen.getByText('2 transactions')).toBeTruthy()
        // Everything fits on one page: no Previous / Next.
        expect(screen.queryByRole('button', { name: 'Next' })).toBeNull()
        expect(screen.getAllByText('Completed')).toHaveLength(2)
    })

    it('shows the error and loads again on "Try again"', async () => {
        get.mockResolvedValueOnce({ data: undefined, error: { status: 500, title: 'Server error' }, response: new Response(null, { status: 500 }) } as GetResult)
        get.mockResolvedValueOnce(page([], 0))
        const user = renderPage()

        expect((await screen.findByRole('alert')).textContent).toContain('Server error')
        await user.click(screen.getByRole('button', { name: 'Try again' }))

        expect(await screen.findByText(/No transactions yet/)).toBeTruthy()
        expect(get).toHaveBeenCalledTimes(2)
    })

    it('asks for the next page', async () => {
        get.mockResolvedValueOnce(page([item(1, 'Sent')], 11, 1))
        get.mockResolvedValueOnce(page([item(0, 'Sent')], 11, 2))
        const user = renderPage()

        await screen.findByText(/Page 1 of 2/)
        expect(screen.getByRole('button', { name: 'Previous' }).hasAttribute('disabled')).toBe(true)
        await user.click(screen.getByRole('button', { name: 'Next' }))

        await screen.findByText(/Page 2 of 2/)
        expect(get).toHaveBeenLastCalledWith('/api/transfers', { params: { query: { page: 2, pageSize: 10 } } })
        expect(screen.getByRole('button', { name: 'Next' }).hasAttribute('disabled')).toBe(true)
    })

    it('shows only received money when "Received" is picked, from page 1', async () => {
        get.mockResolvedValueOnce(page([item(2, 'Received'), item(1, 'Sent')], 2))
        get.mockResolvedValueOnce(page([item(2, 'Received')], 1))
        const user = renderPage()
        await screen.findAllByRole('row')

        await user.click(screen.getByRole('radio', { name: 'Received' }))

        await screen.findByText('1 transaction')
        expect(get).toHaveBeenLastCalledWith('/api/transfers', { params: { query: { page: 1, pageSize: 10, direction: 'Received' } } })
    })

    it('says so when a filter has nothing', async () => {
        get.mockResolvedValueOnce(page([item(1, 'Received')], 1))
        get.mockResolvedValueOnce(page([], 0))
        const user = renderPage()
        await screen.findAllByRole('row')

        await user.click(screen.getByRole('radio', { name: 'Sent' }))

        expect(await screen.findByText("You haven't sent any money yet.")).toBeTruthy()
    })
})
