import { useState } from 'react'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import { todayInBaghdad, useDailySummary, type DailySummary } from './useDailySummary'

export function MerchantSummaryPage() {
  const [date, setDate] = useState(todayInBaghdad)
  const { data, loading, problem, reload } = useDailySummary(date)

  function changeDate(next: string) {
    if (!next) return // the date input is empty while the user is typing
    reload()
    setDate(next)
  }

  return (
    <section>
      <h1>Daily summary</h1>
      <div className="card">
        <label>
          Day (Baghdad time)
          <input type="date" value={date} max={todayInBaghdad()} onChange={(e) => changeDate(e.target.value)} />
        </label>
      </div>

      <div className="card">
        {loading && <p className="muted">Loading summary…</p>}

        {!loading && problem && (
          <>
            <ProblemDetailsAlert problem={problem} />
            <button type="button" onClick={reload}>Try again</button>
          </>
        )}

        {!loading && !problem && data && data.paymentCount === 0 && (
          <p className="muted">No payments on this day.</p>
        )}

        {!loading && !problem && data && data.paymentCount > 0 && <SummaryBody summary={data} />}
      </div>
    </section>
  )
}

function SummaryBody({ summary }: { summary: DailySummary }) {
  const max = Math.max(...summary.byHour.map((h) => h.totalAmount))

  return (
    <>
      <div className="summary-totals">
        <div>
          <span className="muted">Payments</span>
          <p className="balance" aria-label="Payment count">{summary.paymentCount}</p>
        </div>
        <div>
          <span className="muted">Total</span>
          <p className="balance" aria-label="Total received">{formatMoney(summary.totalAmount, summary.currency)}</p>
        </div>
      </div>

      <h2>By hour</h2>
      <div className="bars" role="list" aria-label="Payments by hour">
        {summary.byHour.map((h) => {
          const label = `${String(h.hour).padStart(2, '0')}:00 — ${h.paymentCount} ${h.paymentCount === 1 ? 'payment' : 'payments'}, ${formatMoney(h.totalAmount, summary.currency)}`
          return (
            <div key={h.hour} className="bar" role="listitem" aria-label={label} title={label}>
              <div className="bar-fill" style={{ height: `${max > 0 ? (h.totalAmount / max) * 100 : 0}%` }} />
              <span className="bar-hour">{h.hour}</span>
            </div>
          )
        })}
      </div>
    </>
  )
}
