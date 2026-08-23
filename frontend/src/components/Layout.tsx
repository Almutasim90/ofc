import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/AuthContext'
import LanguageSwitcher from './LanguageSwitcher'
import ThemeToggle from './ThemeToggle'

export default function Layout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { user, logout, hasPermission } = useAuth()

  return (
    <div>
      <header className="top-bar">
        <div className="top-bar-nav">
          {user && hasPermission('users.manage') && <Link to="/users">{t('nav.users')}</Link>}
          {user && hasPermission('branches.manage') && <Link to="/branches">{t('nav.branches')}</Link>}
          {user && hasPermission('products.manage') && <Link to="/products">{t('nav.products')}</Link>}
          {user && hasPermission('products.manage') && <Link to="/raw-materials">{t('nav.rawMaterials')}</Link>}
          {user && hasPermission('inventory.adjust') && <Link to="/inventory">{t('nav.inventory')}</Link>}
        </div>
        <div className="top-bar-actions">
          <ThemeToggle />
          <LanguageSwitcher />
          {user && (
            <button type="button" onClick={logout}>
              {t('nav.logout')}
            </button>
          )}
        </div>
      </header>
      <main className="content">{children}</main>
    </div>
  )
}
