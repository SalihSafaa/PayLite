import { useState } from 'react'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import { useMerchantPayments } from './useMerchantPayments'

const PAGE_SIZE = 10
const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

export function MerchantPaymentsPage() {
  const [page, setPage] = useState(1)
  const { data, loading, problem, reload } = useMerchantPayments(page, PAGE_SIZE)

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  function goTo(next: number) {
    reload()
    setPage(next)
  }

  return (
    <section>
      <h1>Incoming payments</h1>
      <div className="card">
        {loading && <p className="muted">Loading payments…</p>}

        {!loading && problem && (
          <>
            <ProblemDetailsAlert problem={problem} />
            <button type="button" onClick={reload}>Try again</button>
          </>
        )}

        {!loading && !problem && data && data.totalCount === 0 && (
          <p className="muted">No payments yet. Payments from customers show up here.</p>
        )}

        {!loading && !problem && data && data.totalCount > 0 && (
          <>
            <table className="table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>From</th>
                  <th className="num">Amount</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((p) => (
                  <tr key={p.transferId}>
                    <td>{dateFormat.format(new Date(p.createdAt))}</td>
                    <td>{p.fromUsername}</td>
                    <td className="num in">+{formatMoney(p.amount, p.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pager">
              <button type="button" onClick={() => goTo(page - 1)} disabled={page <= 1}>
                Previous
              </button>
              <span className="muted">
                Page {page} of {totalPages} · {data.totalCount} {data.totalCount === 1 ? 'payment' : 'payments'}
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
