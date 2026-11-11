import { ProblemDetailsAlert } from '../../shared/components/ProblemDetailsAlert'
import { formatMoney } from '../../shared/lib/money'
import { TopUpForm } from './TopUpForm'
import { useMyWallet } from './useMyWallet'

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
                {/* TODO (Day 5): last 5 transfers from GET /api/transfers?page=1&pageSize=5 */}
                <p className="muted">No transactions yet.</p>
            </div>
        </section>
    )
}
