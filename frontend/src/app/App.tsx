import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { MerchantPaymentsPage } from '../features/activity/MerchantPaymentsPage'
import { MerchantSummaryPage } from '../features/activity/MerchantSummaryPage'
import { NotificationsPage } from '../features/activity/NotificationsPage'
import { SendMoneyPage } from '../features/wallet/SendMoneyPage'
import { TransactionHistoryPage } from '../features/wallet/TransactionHistoryPage'
import { WalletDashboardPage } from '../features/wallet/WalletDashboardPage'
import { AuthProvider } from '../shared/auth/AuthProvider'
import { ProtectedRoute } from '../shared/auth/ProtectedRoute'
import { Layout } from '../shared/components/Layout'
import { LoginPage } from './LoginPage'
import { RegisterPage } from './RegisterPage'

export function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />

            <Route element={<ProtectedRoute />}>
              {/* Owner A */}
              <Route index element={<WalletDashboardPage />} />
              <Route path="/send" element={<SendMoneyPage />} />
              <Route path="/history" element={<TransactionHistoryPage />} />
              {/* Owner B */}
              <Route path="/notifications" element={<NotificationsPage />} />
            </Route>

            <Route element={<ProtectedRoute roles={['Merchant']} />}>
              <Route path="/merchant/payments" element={<MerchantPaymentsPage />} />
              <Route path="/merchant/summary" element={<MerchantSummaryPage />} />
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
