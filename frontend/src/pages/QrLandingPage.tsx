import { useEffect, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useParams, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { api, apiEndpoint, ApiError, resolveApiAssetUrl } from '../api/client'
import type { QrMenuCategoryDto, QrMenuItemDto, QrSessionDto, RestaurantOrderDto } from '../api/types'
import Money from '../components/Money'

type CartLine = {
  item: QrMenuItemDto
  quantity: number
  notes: string
  modifierOptionIds: string[]
  comboSelections: { comboComponentId: string; selectedMenuItemId: string }[]
  delta: number
}

type ConfigureState = { item: QrMenuItemDto; quantity: number; notes: string; selected: string[] }

export default function QrLandingPage() {
  const { pointId, token: legacyToken } = useParams()
  const [params] = useSearchParams()
  const { t, i18n } = useTranslation()
  const [session, setSession] = useState<QrSessionDto | null>(null)
  const [menu, setMenu] = useState<QrMenuCategoryDto[]>([])
  const [cart, setCart] = useState<CartLine[]>([])
  const [configure, setConfigure] = useState<ConfigureState | null>(null)
  const [order, setOrder] = useState<RestaurantOrderDto | null>(null)
  const [failed, setFailed] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const signedToken = params.get('token')
  const name = (value: { nameAr: string; nameEn: string }) => i18n.language === 'ar' ? value.nameAr : value.nameEn
  const headers = session ? { 'X-QR-Session': session.accessToken } : undefined

  useEffect(() => {
    setSession(null); setMenu([]); setCart([]); setConfigure(null); setOrder(null); setFailed('')
    const path = pointId && signedToken
      ? `/api/qr-ordering/points/${pointId}/resolve?token=${encodeURIComponent(signedToken)}`
      : legacyToken ? `/api/qr-ordering/resolve/${encodeURIComponent(legacyToken)}` : null
    if (!path) { setFailed(t('orderingPoints.invalid')); return }
    let active = true
    void api.get<QrSessionDto>(path).then(async result => {
      const categories = await api.get<QrMenuCategoryDto[]>(`/api/qr-ordering/sessions/${result.sessionId}/menu`, { 'X-QR-Session': result.accessToken })
      if (!active) return
      setSession(result); setMenu(categories)
      const stored = localStorage.getItem(`qr-cart:${result.sessionId}`)
      if (stored) { try { setCart(JSON.parse(stored) as CartLine[]) } catch { localStorage.removeItem(`qr-cart:${result.sessionId}`); setCart([]) } }
      else setCart([])
      void api.get<RestaurantOrderDto>(`/api/qr-ordering/sessions/${result.sessionId}/order`, { 'X-QR-Session': result.accessToken }).then(setOrder).catch(() => {})
    }).catch(error => { if (active) setFailed(error instanceof ApiError ? error.message : t('orderingPoints.invalid')) })
    return () => { active = false }
  }, [legacyToken, pointId, signedToken, t])

  useEffect(() => {
    if (!session) return
    localStorage.setItem(`qr-cart:${session.sessionId}`, JSON.stringify(cart))
  }, [cart, session])

  useEffect(() => {
    if (!session) return
    const connection = new HubConnectionBuilder().withUrl(apiEndpoint('/hubs/qr-orders')).configureLogging(LogLevel.Warning).withAutomaticReconnect().build()
    connection.on('QrOrderStatusChanged', (next: RestaurantOrderDto) => setOrder(next))
    connection.onreconnected(() => connection.invoke('JoinSession', session.sessionId, session.accessToken))
    void connection.start().then(() => connection.invoke('JoinSession', session.sessionId, session.accessToken)).catch(() => {})
    const poll = window.setInterval(() => void api.get<RestaurantOrderDto>(`/api/qr-ordering/sessions/${session.sessionId}/order`, { 'X-QR-Session': session.accessToken }).then(setOrder).catch(() => {}), 15000)
    return () => { window.clearInterval(poll); void connection.stop() }
  }, [session])

  const startConfigure = (item: QrMenuItemDto) => {
    const defaults = item.comboComponents.flatMap(component => component.options.filter(option => option.isDefault).map(option => `${component.id}:${option.menuItemId}`))
    setConfigure({ item, quantity: 1, notes: '', selected: defaults })
  }

  const toggle = (key: string, groupKeys: string[], single: boolean) => setConfigure(current => {
    if (!current) return current
    const exists = current.selected.includes(key)
    const selected = single ? [...current.selected.filter(value => !groupKeys.includes(value)), ...(exists ? [] : [key])] : exists ? current.selected.filter(value => value !== key) : [...current.selected, key]
    return { ...current, selected }
  })

  const addConfigured = () => {
    if (!configure) return
    for (const group of configure.item.modifierGroups) {
      const count = group.options.filter(option => configure.selected.includes(option.id)).length
      const minimum = group.isRequired ? Math.max(1, group.minSelect) : group.minSelect
      if (count < minimum || count > group.maxSelect) { setFailed(`${name(group)}: ${minimum}-${group.maxSelect}`); return }
    }
    for (const component of configure.item.comboComponents) {
      const count = component.options.filter(option => configure.selected.includes(`${component.id}:${option.menuItemId}`)).length
      const minimum = component.isRequired ? Math.max(1, component.minSelect) : component.minSelect
      if (count < minimum || count > component.maxSelect) { setFailed(`${component.slotLabel}: ${minimum}-${component.maxSelect}`); return }
    }
    const modifierOptionIds = configure.item.modifierGroups.flatMap(group => group.options).filter(option => configure.selected.includes(option.id)).map(option => option.id)
    const comboSelections = configure.item.comboComponents.flatMap(component => component.options.filter(option => configure.selected.includes(`${component.id}:${option.menuItemId}`)).map(option => ({ comboComponentId: component.id, selectedMenuItemId: option.menuItemId })))
    const modifierDelta = configure.item.modifierGroups.flatMap(group => group.options).filter(option => configure.selected.includes(option.id)).reduce((sum, option) => sum + option.priceDelta, 0)
    const comboDelta = configure.item.comboComponents.flatMap(component => component.options.map(option => ({ ...option, componentId: component.id }))).filter(option => configure.selected.includes(`${option.componentId}:${option.menuItemId}`)).reduce((sum, option) => sum + option.priceDelta, 0)
    setCart(current => [...current, { item: configure.item, quantity: configure.quantity, notes: configure.notes.trim(), modifierOptionIds, comboSelections, delta: modifierDelta + comboDelta }])
    setConfigure(null); setFailed('')
  }

  const submit = async () => {
    if (!session || !cart.length) return
    setSubmitting(true); setFailed('')
    try {
      const created = await api.post<RestaurantOrderDto>(`/api/qr-ordering/sessions/${session.sessionId}/orders`, { accessToken: session.accessToken, lines: cart.map(line => ({ menuItemId: line.item.id, quantity: line.quantity, notes: line.notes || null, modifierOptionIds: line.modifierOptionIds, comboSelections: line.comboSelections })) }, headers)
      setOrder(created); setCart([]); localStorage.removeItem(`qr-cart:${session.sessionId}`)
      await api.post(`/api/qr-ordering/orders/${created.id}/confirm`, { sessionId: session.sessionId, accessToken: session.accessToken }, headers)
      setOrder({ ...created, status: 'PendingApproval' })
    } catch (error) { setFailed(error instanceof ApiError ? error.message : t('qrOrdering.submitError')) }
    finally { setSubmitting(false) }
  }

  const retryConfirmation = async () => {
    if (!session || !order || order.status !== 'Open') return
    setSubmitting(true); setFailed('')
    try {
      await api.post(`/api/qr-ordering/orders/${order.id}/confirm`, { sessionId: session.sessionId, accessToken: session.accessToken }, headers)
      setOrder({ ...order, status: 'PendingApproval' })
    } catch (error) { setFailed(error instanceof ApiError ? error.message : t('qrOrdering.submitError')) }
    finally { setSubmitting(false) }
  }

  if (failed && !session) return <main className="mx-auto grid min-h-screen max-w-xl place-content-center gap-4 p-6 text-center"><span className="brand-mark mx-auto">O</span><h1>{t('app.title')}</h1><p className="error-text">{failed}</p></main>
  if (!session) return <main className="grid min-h-screen place-content-center"><span className="brand-mark animate-pulse">O</span></main>
  if (order && order.status !== 'Open') return <main className="mx-auto grid min-h-screen max-w-xl place-content-center gap-5 p-6 text-center"><span className="brand-mark mx-auto">O</span><p className="text-sm font-bold uppercase tracking-[.2em]">#{order.orderNumber}</p><h1>{t(`qrOrdering.status.${order.status}`, { defaultValue: order.status })}</h1><Money value={order.grandTotal} /><p className="text-muted">{t('qrOrdering.statusHint')}</p></main>

  const total = cart.reduce((sum, line) => sum + (line.item.price + line.delta) * line.quantity, 0)
  return <main className="mx-auto min-h-screen max-w-6xl p-4 pb-36 sm:p-8 sm:pb-40">
    <header className="mb-8 rounded-3xl bg-primary p-6 text-on-primary shadow-lg"><span className="text-sm font-bold uppercase tracking-[.2em]">{t('app.title')}</span><h1 className="mt-2 text-3xl font-extrabold">{session.label}</h1><p className="mt-1 opacity-80">{t('qrOrdering.choose')}</p></header>
    {failed && <button className="mb-4 w-full border-danger text-danger" onClick={() => setFailed('')}>{failed}</button>}
    {order?.status === 'Open' && <button className="mb-4 w-full" disabled={submitting} onClick={retryConfirmation}>{t('qrOrdering.retryConfirmation')}</button>}
    <div className="grid gap-8">{menu.map(category => <section key={category.id}><h2 className="mb-3 text-xl font-extrabold">{name(category)}</h2><div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">{category.items.map(item => <button className="product-card overflow-hidden p-0 text-start" key={item.id} onClick={() => startConfigure(item)}>{item.imageUrl && <img className="h-32 w-full object-cover sm:h-40" src={resolveApiAssetUrl(item.imageUrl)} alt="" />}<span className="grid gap-1 p-4"><strong>{name(item)}</strong><Money value={item.price} />{item.kind === 'Combo' && <small>{t('restaurant.combo')}</small>}</span></button>)}</div></section>)}</div>
    <aside className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface/95 p-4 shadow-2xl backdrop-blur"><div className="mx-auto flex max-w-6xl items-center justify-between gap-4"><div><strong>{t('cashier.cart')} · {cart.reduce((sum, line) => sum + line.quantity, 0)}</strong><div><Money value={total} /></div></div><button disabled={!cart.length || submitting} onClick={submit}>{submitting ? t('common.loading') : t('qrOrdering.submit')}</button></div>{cart.length > 0 && <div className="mx-auto mt-3 flex max-w-6xl gap-2 overflow-x-auto">{cart.map((line, index) => <button className="button-secondary whitespace-nowrap" key={`${line.item.id}-${index}`} onClick={() => setCart(current => current.filter((_, itemIndex) => itemIndex !== index))}>{line.quantity}× {name(line.item)} · {t('common.delete')}</button>)}</div>}</aside>
    {configure && <div className="app-scrim fixed inset-0 z-50 grid place-items-end sm:place-items-center"><div className="settings-card max-h-[92vh] w-full overflow-auto rounded-b-none sm:w-[min(42rem,94vw)] sm:rounded-2xl"><h2>{name(configure.item)}</h2>{configure.item.modifierGroups.map(group => <fieldset key={group.id}><legend>{name(group)} · {group.minSelect}-{group.maxSelect}</legend>{group.options.map(option => <label className="checkbox-row" key={option.id}><input type={group.maxSelect === 1 ? 'radio' : 'checkbox'} name={group.id} checked={configure.selected.includes(option.id)} onChange={() => toggle(option.id, group.options.map(value => value.id), group.maxSelect === 1)} />{name(option)} <Money value={option.priceDelta} /></label>)}</fieldset>)}{configure.item.comboComponents.map(component => <fieldset key={component.id}><legend>{component.slotLabel} · {component.minSelect}-{component.maxSelect}</legend>{component.options.map(option => { const key = `${component.id}:${option.menuItemId}`; return <label className="checkbox-row" key={key}><input type={component.maxSelect === 1 ? 'radio' : 'checkbox'} name={component.id} checked={configure.selected.includes(key)} onChange={() => toggle(key, component.options.map(value => `${component.id}:${value.menuItemId}`), component.maxSelect === 1)} />{name(option)} <Money value={option.priceDelta} /></label> })}</fieldset>)}<label>{t('qrOrdering.quantity')}<input type="number" min="1" max="50" value={configure.quantity} onChange={event => setConfigure(current => current && ({ ...current, quantity: Math.max(1, Math.min(50, Number(event.target.value))) }))} /></label><label>{t('qrOrdering.notes')}<textarea maxLength={500} value={configure.notes} onChange={event => setConfigure(current => current && ({ ...current, notes: event.target.value }))} /></label><div className="modal-actions"><button className="button-secondary" onClick={() => setConfigure(null)}>{t('common.cancel')}</button><button onClick={addConfigured}>{t('orders.add')}</button></div></div></div>}
  </main>
}
