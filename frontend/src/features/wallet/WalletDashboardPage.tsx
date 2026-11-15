import { Link } from 'react-router'
import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import { TopUpForm } from './TopUpForm'
import { TransferTable } from './TransferTable'
import { useMyWallet } from './useMyWallet'
import { useTransferHistory } from './useTransferHistory'

function RecentTransactions() {
    const { data, loading, problem, reload } = useTransferHistory(1, 5)

    if (loading) return <p className="muted">Loading transactions…</p>
    if (problem) {
        return (
            <>
                <ProblemDetailsAlert problem={problem} />
                <button type="button" onClick={reload}>Try again</button>
            </>
        )
    }
    if (!data || data.totalCount === 0) {
        return (
            <p className="muted">
                No transactions yet. <Link to="/send">Send money</Link>
            </p>
        )
    }
    return (
        <>
            <TransferTable items={data.items} />
            {data.totalCount > data.items.length && <Link to="/history">See all {data.totalCount} transactions</Link>}
        </>
    )
}

export function WalletDashboardPage() {
    const { wallet, setWallet, loading, problem, reload } = useMyWallet()

    if (loading && !wallet) return <p className="muted">Loading your wallet…</p>

    if (problem && !wallet) {
        return (
            <section>
                <ProblemDetailsAlert problem={problem} />
                <button type="button" onClick={reload}>Try again</button>
            </section>
        )
    }

    if (!wallet) return null

    return (
        <section>
            <h1>Wallet</h1>
            <div className="card">
                <span className="muted">Balance</span>
                {wallet.isMerchant && <span className="badge">Merchant</span>}
                <p className="balance">{formatMoney(wallet.balance, wallet.currency)}</p>
                <TopUpForm onToppedUp={setWallet} />
            </div>
            <div className="card">
                <h2>Recent transactions</h2>
                <RecentTransactions />
            </div>
        </section>
    )
}
