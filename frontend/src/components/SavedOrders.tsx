import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { CreateSaleRequest, ProductDto, ProductChannelPriceDto, SaleDto, SaleEditDto, SaleItemDto } from '../api/types'
import Money from './Money'
import PaymentSelector from './PaymentSelector'
import { paymentAmounts, roundMoney, validPayment, type PaymentMethod } from '../utils/payments'

const discount = (total: number, type: string, value: number) => roundMoney(Math.max(0, total - Math.min(total, type === 'Percentage' ? total * value / 100 : type === 'FixedAmount' ? value : 0)))

export default function SavedOrders({ branchId, products, onClose }: { branchId: string; products: ProductDto[]; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const dialog = useRef<HTMLDialogElement>(null)
  useEffect(() => { const element = dialog.current; element?.showModal(); return () => element?.close() }, [])
  const [sales, setSales] = useState<SaleDto[]>([])
  const [editing, setEditing] = useState<SaleDto | null>(null)
  const [lines, setLines] = useState<SaleItemDto[]>([])
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [cash, setCash] = useState('')
  const [reason, setReason] = useState('')
  const [history, setHistory] = useState<SaleEditDto[] | null>(null)
  const [prices, setPrices] = useState<Record<string, number>>({})
  const [productId, setProductId] = useState('')
  const [busy, setBusy] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  useEffect(() => {
    let active = true
    api.get<SaleDto[]>(`/api/sales/editable?branchId=${branchId}`).then(rows => { if (active) setSales(rows) })
      .catch(() => { if (active) setError(t('orders.loadError')) }).finally(() => { if (active) setBusy(false) })
    return () => { active = false }
  }, [branchId, t])
  const startEdit = async (sale: SaleDto) => {
    setBusy(true); setError(''); setMessage(''); setHistory(null)
    try {
      const catalog = await api.get<ProductChannelPriceDto[]>(`/api/channels/${sale.channelId}/catalog-prices`)
      setPrices(Object.fromEntries(catalog.filter(p => p.price != null).map(p => [p.productId, p.price!])))
      setEditing(sale); setLines(sale.items.map(line => ({ ...line }))); setMethod(sale.paymentMethod)
      setCash(String(sale.cashAmount)); setReason(''); setProductId('')
    } catch { setError(t('orders.loadError')) } finally { setBusy(false) }
  }
  const subtotal = lines.reduce((sum, line) => sum + discount(line.unitPriceSnapshot * line.quantity, line.discountType, line.discountValue), 0)
  const total = editing ? discount(subtotal, editing.discountType, editing.discountValue) : 0
  const save = async () => {
    if (!editing || busy) return
    setBusy(true); setError('')
    const sale: CreateSaleRequest = { branchId, channelId: editing.channelId, paymentMethod: method,
      ...paymentAmounts(method, total, cash), discountType: editing.discountType, discountValue: editing.discountValue,
      lines: lines.map(l => ({ productId: l.productId, quantity: l.quantity, discountType: l.discountType, discountValue: l.discountValue })) }
    try {
      const updated = await api.put<SaleDto>(`/api/sales/${editing.id}`, { sale, reason, revision: editing.revision })
      setSales(rows => rows.map(row => row.id === updated.id ? updated : row)); setEditing(null); setMessage(t('orders.saved'))
    } catch (e) { setError(e instanceof ApiError && e.status === 409 ? t('orders.conflict') : t('orders.saveError')) }
    finally { setBusy(false) }
  }
  const showHistory = async (sale: SaleDto) => {
    setBusy(true); setError(''); setMessage('')
    try { setHistory(await api.get<SaleEditDto[]>(`/api/sales/${sale.id}/history`)) }
    catch { setError(t('orders.loadError')) } finally { setBusy(false) }
  }
  const snapshot = (sale: SaleDto) => <div className="order-snapshot"><ul>{sale.items.map(item => <li key={item.productId}>{item.productNameSnapshot} × {item.quantity} · <Money value={item.lineTotal} /></li>)}</ul><p>{t('cashier.total')}: <Money value={sale.totalAmount} /> · {t('cashier.cash')}: <Money value={sale.cashAmount} /> · {t('cashier.card')}: <Money value={sale.cardAmount} /></p></div>
  return <dialog ref={dialog} className="saved-orders-dialog" aria-labelledby="saved-orders-title" onCancel={event => { event.preventDefault(); if (!busy) onClose() }}>
    <header><h2 id="saved-orders-title">{editing ? t('orders.edit') : t('orders.title')}</h2><button type="button" disabled={busy} onClick={onClose}>{t('orders.close')}</button></header>
    {error && <p role="alert" className="error-text">{error}</p>}{message && <p role="status">{message}</p>}
    {busy && <p role="status">{t('common.loading')}</p>}
    {editing ? <>
      <p>{t('orders.editHint')}</p>
      <div className="order-lines">{lines.map(line => <div className="order-line" key={line.productId}>
        <span>{line.productNameSnapshot} · <Money value={line.unitPriceSnapshot} /></span>
        <label>{t('orders.quantity')}<input type="number" min="0.001" step="0.001" value={line.quantity} disabled={busy} onChange={e => setLines(current => current.map(l => l.productId === line.productId ? { ...l, quantity: Number(e.target.value) } : l))} /></label>
        <button type="button" disabled={busy} onClick={() => setLines(current => current.filter(l => l.productId !== line.productId))}>{t('orders.remove')}</button>
      </div>)}</div>
      <div className="order-add-line"><select aria-label={t('orders.addProduct')} value={productId} disabled={busy} onChange={e => setProductId(e.target.value)}><option value="">{t('orders.addProduct')}</option>{products.filter(p => !lines.some(l => l.productId === p.id)).map(p => <option key={p.id} value={p.id}>{i18n.language === 'ar' ? p.nameAr : p.nameEn}</option>)}</select>
        <button type="button" disabled={busy || !productId} onClick={() => { const p = products.find(p => p.id === productId); if (!p) return; setLines(current => [...current, { productId: p.id, productNameSnapshot: p.nameAr, unitPriceSnapshot: prices[p.id] ?? p.price, quantity: 1, lineTotal: prices[p.id] ?? p.price, discountType: 'None', discountValue: 0 }]); setProductId('') }}>{t('orders.add')}</button></div>
      <p>{t('cashier.total')}: <Money value={total} /> · {t('orders.difference')}: <Money value={roundMoney(total - editing.totalAmount)} /></p>
      <PaymentSelector method={method} cash={cash} total={total} onMethod={setMethod} onCash={setCash} disabled={busy} />
      <label>{t('orders.reason')}<textarea maxLength={1000} value={reason} disabled={busy} onChange={e => setReason(e.target.value)} /></label>
      <p>{t('orders.settlementHint')}</p>
      <footer><button type="button" disabled={busy || !reason.trim() || !lines.length || lines.some(l => !Number.isFinite(l.quantity) || l.quantity <= 0 || roundMoney(l.quantity) !== l.quantity) || !validPayment(method, total, cash)} onClick={save}>{t('orders.save')}</button><button type="button" disabled={busy} onClick={() => { setEditing(null); setError('') }}>{t('orders.back')}</button></footer>
    </> : history !== null ? <>
      <h3>{t('orders.history')}</h3>{!history.length && <p>{t('orders.noHistory')}</p>}
      {history.map(entry => <article className="order-history-entry" key={entry.id}><strong>{entry.editedByName}</strong><time>{new Date(entry.createdAt).toLocaleString(i18n.language)}</time><p>{entry.reason}</p><h4>{t('orders.before')}</h4>{snapshot(entry.before)}<h4>{t('orders.after')}</h4>{snapshot(entry.after)}</article>)}
      <button type="button" disabled={busy} onClick={() => setHistory(null)}>{t('orders.back')}</button>
    </> : <>
      <p>{t('orders.listHint')}</p>{!busy && !sales.length && <p>{t('orders.empty')}</p>}
      {sales.map(sale => <article className="saved-order" key={sale.id}><div><strong>#{sale.saleNumber}</strong><time>{new Date(sale.createdAt).toLocaleString(i18n.language)}</time><Money value={sale.totalAmount} /><span>{t(`cashier.${sale.paymentMethod.toLowerCase()}`)}</span></div>
        <div><button type="button" disabled={busy || !sale.canEdit} onClick={() => startEdit(sale)}>{t('orders.edit')}</button><button type="button" disabled={busy} onClick={() => showHistory(sale)}>{t('orders.history')}</button></div></article>)}
    </>}
  </dialog>
}
