import { Navigate } from 'react-router-dom'
import { useAuth } from './AuthContext'

/// Sends the user to the first screen they actually have access to.
export default function HomeRedirect() {
  const { user, hasPermission } = useAuth()

  if (!user) return <Navigate to="/login" replace />
  if (hasPermission('sales.create')) return <Navigate to="/cashier" replace />
  if (hasPermission('users.manage')) return <Navigate to="/users" replace />
  if (hasPermission('inventory.adjust')) return <Navigate to="/inventory" replace />

  return <Navigate to="/login" replace />
}
