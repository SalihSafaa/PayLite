import { formatMoney } from '../../shared/lib/money'
import type { TransferHistoryItem } from './useTransferHistory'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

/** Shared by the dashboard (last 5) and the history page. */
export function TransferTable({ items }: { items: TransferHistoryItem[] }) {
    return (
        <table className="table">
            <thead>
                <tr>
                    <th>Date</th>
                    <th>Type</th>
                    <th>With</th>
                    <th className="num">Amount</th>
                    <th className="num">Balance after</th>
                </tr>
            </thead>
            <tbody>
                {items.map((item) => {
                    const sent = item.direction === 'Sent'
                    return (
                        <tr key={item.transferId}>
                            <td>{dateFormat.format(new Date(item.createdAt))}</td>
                            <td>{sent ? 'Sent' : 'Received'}</td>
                            <td>{item.counterpartyUsername}</td>
                            <td className={`num ${sent ? 'out' : 'in'}`}>
                                {sent ? '−' : '+'}
                                {formatMoney(item.amount, item.currency)}
                            </td>
                            <td className="num muted">
                                {item.balanceAfter != null ? formatMoney(item.balanceAfter, item.currency) : '—'}
                            </td>
                        </tr>
                    )
                })}
            </tbody>
        </table>
    )
}
