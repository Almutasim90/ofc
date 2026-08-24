import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import type { ShiftDto, UpcomingClosingDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { Link } from 'react-router-dom'

interface AppNotification { id: string; text: string; tone: 'info' | 'warning' }

export default function NotificationBell() {
  const { t } = useTranslation()
  const { hasPermission } = useAuth()
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<AppNotification[]>([])
  const rootRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!hasPermission('sales.create') && !hasPermission('inventory.adjust')) return
    let active = true
    const load = async () => {
      try {
        const operational = hasPermission('sales.create') ? await Promise.all([api.get<ShiftDto | undefined>('/api/shifts/current'), api.get<UpcomingClosingDto>('/api/closing-schedule/upcoming')]) : [undefined, undefined]
        const alerts = hasPermission('inventory.adjust') ? await api.get<Array<{id:string;materialNameAr:string;materialNameEn:string}>>('/api/notifications/low-stock') : []
        const [shift, closing] = operational
        if (!active) return
        const next: AppNotification[] = []
        if (shift) next.push({ id: 'shift-open', text: t('notifications.shiftOpen'), tone: 'info' })
        if (closing?.warning) next.unshift({ id: 'closing', text: t('notifications.closingSoon', { minutes: closing.minutesRemaining }), tone: 'warning' })
        alerts.forEach(alert => next.unshift({id:alert.id,text:t('notifications.lowStock',{material:alert.materialNameAr}),tone:'warning'}))
        setItems(next)
      } catch {
        if (active) setItems([])
      }
    }
    void load()
    const timer = window.setInterval(load, 60_000)
    return () => { active = false; window.clearInterval(timer) }
  }, [hasPermission, t])

  useEffect(() => {
    const close = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [])

  return <div className="notification-bell" ref={rootRef}>
    <button type="button" className="notification-trigger" onClick={() => setOpen((value) => !value)} aria-label={t('notifications.title')} aria-expanded={open}>
      <BellIcon />
      {items.length > 0 && <span className="notification-badge">{items.length}</span>}
    </button>
    {open && <div className="notification-panel">
      <strong>{t('notifications.title')}</strong>
      {items.length === 0
        ? <p>{t('notifications.empty')}</p>
        : <div className="notification-list">{items.map((item) => <div key={item.id} className={`notification-item is-${item.tone}`}><span />{item.text}</div>)}</div>}
      {hasPermission('inventory.adjust') && <Link to="/notifications" onClick={()=>setOpen(false)}>{t('notifications.viewAll')}</Link>}
    </div>}
  </div>
}

function BellIcon() {
  return <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></svg>
}
