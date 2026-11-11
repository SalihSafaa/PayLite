import { Navigate, Outlet, useLocation } from 'react-router'
import type { Role } from './jwt'
import { useAuth } from './useAuth'

/** Renders the child routes only for a logged-in user (optionally with one of `roles`). */
export function ProtectedRoute({ roles }: { roles?: Role[] }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (roles && !roles.includes(user.role)) return <Navigate to="/" replace />
  return <Outlet />
}
