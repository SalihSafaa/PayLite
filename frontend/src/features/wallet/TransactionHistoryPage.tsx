import { useState } from 'react'
import { Link } from 'react-router'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { TransferTable } from './TransferTable'
import { useTransferHistory } from './useTransferHistory'

const PAGE_SIZE = 10

export function TransactionHistoryPage() {
    const [page, setPage] = useState(1)
    const { data, loading, problem, reload } = useTransferHistory(page, PAGE_SIZE)

    const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

    function goTo(next: number) {
        // Show "Loading…" while the next page is fetched (the effect runs after this render).
        reload()
        setPage(next)
    }

    return (
        <section>
            <h1>Transaction history</h1>
            <div className="card">
                {loading && <p className="muted">Loading transactions…</p>}

                {!loading && problem && (
                    <>
                        <ProblemDetailsAlert problem={problem} />
                        <button type="button" onClick={reload}>Try again</button>
                    </>
                )}

                {!loading && !problem && data && data.totalCount === 0 && (
                    <p className="muted">
                        No transactions yet. <Link to="/send">Send money</Link> to get started.
                    </p>
                )}

                {!loading && !problem && data && data.totalCount > 0 && (
                    <>
                        <TransferTable items={data.items} />
                        <div className="pager">
                            <button type="button" onClick={() => goTo(page - 1)} disabled={page <= 1}>
                                Previous
                            </button>
                            <span className="muted">
                                Page {page} of {totalPages} · {data.totalCount} {data.totalCount === 1 ? 'transaction' : 'transactions'}
                            </span>
                            <button type="button" onClick={() => goTo(page + 1)} disabled={page >= totalPages}>
                                Next
                            </button>
                        </div>
                    </>
                )}
            </div>
        </section>
    )
}
