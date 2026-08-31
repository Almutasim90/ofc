import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/AuthContext'
import LanguageSwitcher from './LanguageSwitcher'
import ThemeToggle from './ThemeToggle'
import NotificationBell from './NotificationBell'
import AppIcon, { type AppIconName } from './AppIcon'
import Breadcrumb, { type BreadcrumbItem } from './Breadcrumb'
import BottomSheet from './BottomSheet'

const BOTTOM_NAV_PRIMARY_COUNT = 4

interface NavItem { to: string; label: string; icon: AppIconName; permission?: string }

function getBreadcrumbTrail(pathname: string, navItems: NavItem[], t: (key: string) => string): BreadcrumbItem[] {
  if (pathname.startsWith('/products/') && pathname.endsWith('/recipe'))
    return [{ label: t('nav.products'), to: '/products' }, { label: t('recipe.title') }]
  if (pathname.startsWith('/users/') && pathname.endsWith('/permissions'))
    return [{ label: t('nav.users'), to: '/users' }, { label: t('permissions.title') }]
  const current = navItems.find((item) => item.to === pathname)
  return current ? [{ label: current.label }] : []
}

function LogoutIcon() {
  return <AppIcon className="h-5 w-5" name="logout" />
}

function UserSettingsLink({ fullName, roleName }: { fullName: string; roleName: string }) {
  const { t } = useTranslation()
  const translatedRole = t(`roles.${roleName}`, { defaultValue: roleName })
  return <Link className="user-settings-link flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-xl bg-surface2 p-1 text-text sm:justify-start sm:px-2" to="/settings" title={fullName}><span className="text-on-primary flex h-8 w-8 flex-none items-center justify-center rounded-full bg-primary text-sm font-bold">{fullName.trim().charAt(0).toUpperCase()}</span><span className="hidden min-w-0 text-start sm:block"><span className="block max-w-32 truncate text-sm font-bold xl:max-w-40">{fullName}</span><span className="hidden max-w-40 truncate text-xs font-normal text-muted lg:block">{translatedRole}</span></span></Link>
}

export default function Layout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const { user, logout, hasPermission } = useAuth()
  const [moreOpen, setMoreOpen] = useState(false)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => localStorage.getItem('sidebar-collapsed') === 'true')
  const [kiosk, setKiosk] = useState(false)
  const kioskRequested = useRef(false)
  const printing = useRef(false)
  const restoreKioskAfterPrint = useRef(false)

  useEffect(() => {
    const requestKiosk = () => {
      if (kioskRequested.current && !document.fullscreenElement)
        document.documentElement.requestFullscreen?.().catch(() => {})
    }
    const syncKioskState = () => {
      const isFullscreen = !!document.fullscreenElement
      setKiosk(isFullscreen)
      if (isFullscreen) restoreKioskAfterPrint.current = false
      // Escape is an explicit exit. Printing is not, so retain the user's
      // kiosk preference until the print dialog closes.
      if (!isFullscreen && !printing.current) kioskRequested.current = false
    }
    const beforePrint = () => { printing.current = true }
    const afterPrint = () => {
      printing.current = false
      restoreKioskAfterPrint.current = kioskRequested.current && !document.fullscreenElement
      requestKiosk()
    }
    const retryKiosk = () => {
      if (restoreKioskAfterPrint.current) requestKiosk()
    }
    document.addEventListener('fullscreenchange', syncKioskState)
    document.addEventListener('click', retryKiosk)
    window.addEventListener('beforeprint', beforePrint)
    window.addEventListener('afterprint', afterPrint)
    return () => {
      document.removeEventListener('fullscreenchange', syncKioskState)
      document.removeEventListener('click', retryKiosk)
      window.removeEventListener('beforeprint', beforePrint)
      window.removeEventListener('afterprint', afterPrint)
    }
  }, [])

  useEffect(() => {
    if (user) return
    kioskRequested.current = false
    restoreKioskAfterPrint.current = false
    setKiosk(false)
    if (document.fullscreenElement) document.exitFullscreen().catch(() => {})
  }, [user])

  const toggleKiosk = async () => {
    if (document.fullscreenElement) {
      kioskRequested.current = false
      restoreKioskAfterPrint.current = false
      await document.exitFullscreen().catch(() => {})
    } else {
      kioskRequested.current = true
      await document.documentElement.requestFullscreen?.().catch(() => {
        kioskRequested.current = false
      })
    }
  }

  const handleLogout = async () => {
    kioskRequested.current = false
    restoreKioskAfterPrint.current = false
    if (document.fullscreenElement) await document.exitFullscreen().catch(() => {})
    logout()
  }

  if (!user || pathname === '/login') return <main>{children}</main>

  const operational = pathname === '/cashier'
  const navItems = ([
    { to: '/cashier', label: t('nav.cashier'), icon: 'cashier', permission: 'sales.create' },
    { to: '/restaurant-orders', label: t('nav.restaurantOrders'), icon: 'cashier', permission: 'orders.create' },
    { to: '/order-cancellations', label: t('nav.orderCancellations'), icon: 'reports', permission: 'orders.cancel' },
    { to: '/shift', label: t('nav.shift'), icon: 'shift', permission: 'sales.create' },
    { to: '/reports', label: t('nav.reports'), icon: 'reports', permission: 'reports.branch.view' },
    { to: '/ai-insights', label: t('nav.aiInsights'), icon: 'ai', permission: 'reports.branch.view' },
    { to: '/inventory', label: t('nav.inventory'), icon: 'inventory', permission: 'inventory.adjust' },
    { to: '/restaurant-inventory', label: 'مخزون المطعم / Restaurant inventory', icon: 'inventory', permission: 'inventory.adjust' },
    { to: '/products', label: t('nav.products'), icon: 'products', permission: 'products.manage' },
    { to: '/restaurant-catalog', label: t('nav.restaurantCatalog'), icon: 'products', permission: 'products.manage' },
    { to: '/printers', label: t('nav.printers'), icon: 'printer', permission: 'products.manage' },
    { to: '/modifiers', label: t('nav.modifiers'), icon: 'products', permission: 'modifiers.manage' },
    { to: '/raw-materials', label: t('nav.rawMaterials'), icon: 'materials', permission: 'products.manage' },
    { to: '/branches', label: t('nav.branches'), icon: 'branches', permission: 'branches.manage' },
    { to: '/users', label: t('nav.users'), icon: 'users', permission: 'users.manage' },
    { to: '/closing-schedule', label: t('nav.closingSchedule'), icon: 'schedule', permission: 'closing.configure' },
    { to: '/channels', label: t('nav.channels'), icon: 'channels', permission: 'channels.manage' },
    { to: '/ai-settings', label: t('nav.aiSettings'), icon: 'ai', permission: 'ai.manage' },
    { to: '/email-settings', label: t('nav.emailSettings'), icon: 'email', permission: 'email.manage' },
    { to: '/receipt-settings', label: t('nav.receiptSettings'), icon: 'printer', permission: 'receipt.manage' },
    { to: '/notifications', label: t('nav.notifications'), icon: 'notifications', permission: 'inventory.adjust' },
    { to: '/settings', label: t('nav.settings'), icon: 'settings' },
  ] satisfies NavItem[]).filter((item) => !item.permission || hasPermission(item.permission))
  const translatedRole = t(`roles.${user.roleName}`, { defaultValue: user.roleName })
  const breadcrumbTrail = getBreadcrumbTrail(pathname, navItems, t)
  const kioskButton = <button className="utility-icon-button" type="button" onClick={toggleKiosk} aria-pressed={kiosk} aria-label={t(kiosk ? 'kiosk.exit' : 'kiosk.enter')} title={t(kiosk ? 'kiosk.exit' : 'kiosk.enter')}><AppIcon className="h-5 w-5" name={kiosk ? 'fullscreenExit' : 'fullscreen'} /></button>

  if (operational) return <div className="operational-shell flex flex-col overflow-hidden bg-bg"><header className="top-bar h-16 flex-none"><Link className="hidden items-center gap-2 font-cairo text-xl font-extrabold text-primary xl:flex" to="/"><span className="brand-mark">O</span>{t('app.title')}</Link><nav className="top-bar-nav">{navItems.slice(0, 2).map((item) => <NavLink key={item.to} to={item.to} aria-label={item.label} title={item.label}><AppIcon className="top-bar-nav-icon h-5 w-5" name={item.icon} /><span>{item.label}</span></NavLink>)}</nav><div className="top-bar-actions"><NotificationBell /><ThemeToggle />{kioskButton}<LanguageSwitcher /><UserSettingsLink fullName={user.fullName} roleName={user.roleName} /><button className="logout-button" onClick={handleLogout} aria-label={t('nav.logout')} title={t('nav.logout')}><LogoutIcon /></button></div></header><main className="min-h-0 flex-1 overflow-hidden">{children}</main></div>

  const primaryNavItems = navItems.slice(0, BOTTOM_NAV_PRIMARY_COUNT)
  const moreNavItems = navItems.slice(BOTTOM_NAV_PRIMARY_COUNT)

  return <div className="admin-shell min-h-screen bg-bg text-text">
    <aside id="app-sidebar" className={`app-sidebar fixed inset-y-0 start-0 z-40 hidden flex-none flex-col transition-all duration-300 lg:flex ${sidebarCollapsed ? 'lg:w-20' : 'w-64'}`}>
      <div className="sidebar-brand flex h-16 items-center justify-between gap-2 border-b px-4"><div className={`min-w-0 items-center gap-2 ${sidebarCollapsed ? 'flex lg:hidden' : 'flex'}`}><span className="brand-mark">O</span><div className="truncate font-cairo text-xl font-extrabold">{t('app.title')}</div></div><button className="sidebar-collapse hidden h-11 w-11 flex-none items-center justify-center p-0 lg:flex" onClick={() => setSidebarCollapsed((value) => { localStorage.setItem('sidebar-collapsed', String(!value)); return !value })} aria-label={sidebarCollapsed ? t('nav.expandMenu') : t('nav.collapseMenu')} title={sidebarCollapsed ? t('nav.expandMenu') : t('nav.collapseMenu')}><AppIcon className={`h-5 w-5 transition-transform ${sidebarCollapsed ? 'rotate-180' : ''}`} name="chevron" /></button></div>
      <nav className="flex flex-1 flex-col gap-1 overflow-y-auto p-3">{navItems.map((item) => <NavLink key={item.to} to={item.to} title={sidebarCollapsed ? item.label : undefined} className={({ isActive }) => `sidebar-nav-link flex min-h-11 items-center gap-3 rounded-xl px-3 py-3 text-sm ${sidebarCollapsed ? 'lg:justify-center' : ''} ${isActive ? 'is-active' : ''}`}><AppIcon className="h-5 w-5 flex-none" name={item.icon} /><span className={sidebarCollapsed ? 'lg:hidden' : ''}>{item.label}</span></NavLink>)}</nav>
      <div className="sidebar-footer grid gap-3 border-t p-4"><div className={`flex min-w-0 items-center gap-3 ${sidebarCollapsed ? 'lg:justify-center' : ''}`}><span className="sidebar-avatar flex h-10 w-10 flex-none items-center justify-center rounded-full font-bold">{user.fullName.trim().charAt(0).toUpperCase()}</span><div className={`min-w-0 ${sidebarCollapsed ? 'lg:hidden' : ''}`}><div className="truncate text-sm font-bold">{user.fullName}</div><div className="sidebar-role truncate text-xs">{translatedRole}</div></div></div><button className="sidebar-logout flex w-full items-center justify-center rounded-xl" onClick={handleLogout} aria-label={t('nav.logout')} title={t('nav.logout')}><LogoutIcon /></button></div>
    </aside>
    <div className={`min-h-screen transition-[margin] duration-300 ${sidebarCollapsed ? 'lg:ms-20' : 'lg:ms-64'}`}><header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-border bg-surface/95 px-4 backdrop-blur lg:px-6"><div className="font-cairo font-bold">{navItems.find((item) => pathname.startsWith(item.to))?.label ?? t('app.title')}</div><div className="top-bar-actions"><NotificationBell /><ThemeToggle />{kioskButton}<LanguageSwitcher /><UserSettingsLink fullName={user.fullName} roleName={user.roleName} /><button className="logout-button" onClick={handleLogout} aria-label={t('nav.logout')} title={t('nav.logout')}><LogoutIcon /></button></div></header><main className="admin-content mx-auto w-full max-w-[1440px] p-4 pb-24 lg:p-6"><Breadcrumb items={breadcrumbTrail} />{children}</main></div>
    <nav className="admin-bottom-nav fixed inset-x-0 bottom-0 z-40 flex border-t border-border bg-surface lg:hidden" aria-label={t('app.title')}>
      {primaryNavItems.map((item) => <NavLink key={item.to} to={item.to} className={({ isActive }) => `flex min-h-14 flex-1 flex-col items-center justify-center gap-0.5 text-ui-xs ${isActive ? 'font-bold text-primary' : 'text-muted'}`}><AppIcon className="h-5 w-5" name={item.icon} /><span className="truncate">{item.label}</span></NavLink>)}
      {moreNavItems.length > 0 && <button type="button" className="flex min-h-14 flex-1 flex-col items-center justify-center gap-0.5 text-ui-xs text-muted" onClick={() => setMoreOpen(true)} aria-haspopup="true" aria-expanded={moreOpen}><AppIcon className="h-5 w-5" name="more" /><span className="truncate">{t('nav.more')}</span></button>}
    </nav>
    <BottomSheet open={moreOpen} onClose={() => setMoreOpen(false)}>
      <div className="grid gap-1">{moreNavItems.map((item) => <NavLink key={item.to} to={item.to} onClick={() => setMoreOpen(false)} className={({ isActive }) => `flex min-h-11 items-center gap-3 rounded-xl px-3 py-3 text-sm ${isActive ? 'bg-surface2 font-bold text-text' : 'text-muted'}`}><AppIcon className="h-5 w-5 flex-none" name={item.icon} /><span>{item.label}</span></NavLink>)}</div>
    </BottomSheet>
  </div>
}
