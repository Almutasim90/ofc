import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from './AuthContext'

export default function ProtectedRoute({
  permission,
  children,
}: {
  permission?: string
  children: ReactNode
}) {
  const { t } = useTranslation()
  const { user, hasPermission } = useAuth()

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (permission && !hasPermission(permission)) {
    return <p>{t('common.unauthorized')}</p>
  }

  return <>{children}</>
}
