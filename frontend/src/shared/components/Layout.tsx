import { NavLink, Outlet } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { NotificationBell } from '../../features/activity/NotificationBell'

export function Layout() {
  const { user, signOut } = useAuth()
  const isMerchant = user?.role === 'Merchant'

  return (
    <div className="layout">
      <header className="topbar">
        <strong className="brand">PayLite</strong>
        {user && (
          <nav>
            {/* Owner A */}
            <NavLink to="/" end>Wallet</NavLink>
            <NavLink to="/send">Send money</NavLink>
            <NavLink to="/history">History</NavLink>
            {/* Owner B */}
            <NavLink to="/notifications">Notifications</NavLink>
            {isMerchant && <NavLink to="/merchant/payments">Payments</NavLink>}
            {isMerchant && <NavLink to="/merchant/summary">Daily summary</NavLink>}
          </nav>
        )}
        {user && (
          <div className="topbar-right">
            <NotificationBell />
            <span className="muted">{user.username}</span>
            <button type="button" onClick={signOut}>Log out</button>
          </div>
        )}
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  )
}
