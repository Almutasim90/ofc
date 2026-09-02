import { FormEvent, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BillSplitDto, BranchDto, OrderEditLogDto, OrderPaymentDto, PaymentMethodDto, RestaurantOrderDto } from '../api/types'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'
import { useAuth } from '../auth/AuthContext'

export default function OrderPaymentsPage() {
  const { t, i18n } = useTranslation()
  const { hasPermission } = useAuth()
  const toast = useToast()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [orders, setOrders] = useState<RestaurantOrderDto[]>([])
  const [orderId, setOrderId] = useState('')
  const [methods, setMethods] = useState<PaymentMethodDto[]>([])
  const [payments, setPayments] = useState<OrderPaymentDto[]>([])
  const [splits, setSplits] = useState<BillSplitDto[]>([])
  const [edits, setEdits] = useState<OrderEditLogDto[]>([])
  const [method, setMethod] = useState('CASH')
  const [amount, setAmount] = useState(0)
  const [selectedSplitId, setSelectedSplitId] = useState('')
  const [shareCount, setShareCount] = useState(2)
  const [splitName, setSplitName] = useState('')
  const [itemQuantities, setItemQuantities] = useState<Record<string, number>>({})
  const [delta, setDelta] = useState(0)
  const [notes, setNotes] = useState('')
  const name = (x: { nameAr: string; nameEn: string }) => i18n.language === 'ar' ? x.nameAr : x.nameEn
  const selected = orders.find(x => x.id === orderId)
  const selectedSplit = splits.find(x => x.id === selectedSplitId)
  const paid = payments.reduce((sum, payment) => sum + payment.amount, 0)

  useEffect(() => {
    void Promise.all([api.get<BranchDto[]>('/api/branches'), api.get<PaymentMethodDto[]>('/api/order-payments/methods')])
      .then(([branchRows, methodRows]) => { setBranches(branchRows); setBranchId(branchRows[0]?.id ?? ''); setMethods(methodRows) })
  }, [])
  useEffect(() => {
    setOrders([]); setOrderId(''); setPayments([]); setSplits([]); setEdits([]); setAmount(0)
    if (!branchId) return
    let active = true
    void api.get<RestaurantOrderDto[]>(`/api/restaurant-orders?branchId=${branchId}`)
      .then(rows => { if (active) { setOrders(rows); setOrderId(rows[0]?.id ?? '') } })
    return () => { active = false }
  }, [branchId])
  useEffect(() => {
    setSelectedSplitId(''); setItemQuantities({}); setPayments([]); setSplits([]); setEdits([]); setAmount(0)
    if (!orderId) return
    let active = true
    void Promise.all([
      api.get<OrderPaymentDto[]>(`/api/order-payments/${orderId}`),
      api.get<BillSplitDto[]>(`/api/order-payments/${orderId}/splits`),
      api.get<OrderEditLogDto[]>(`/api/order-payments/${orderId}/edits`),
    ]).then(([paymentRows, splitRows, editRows]) => { if (active) { setPayments(paymentRows); setSplits(splitRows); setEdits(editRows) } })
    return () => { active = false }
  }, [orderId])

  const errorMessage = (error: unknown) => error instanceof ApiError ? error.message : t('common.saveError')
  const createEqual = async (event: FormEvent) => {
    event.preventDefault()
    try {
      const created = await api.post<BillSplitDto[]>(`/api/order-payments/${orderId}/splits/equal`, { shareCount })
      setSplits(current => [...current, ...created])
      setSelectedSplitId(created[0]?.id ?? '')
      setAmount(created[0]?.remainingAmount ?? 0)
      toast.success(t('orderPayments.splitCreated'))
    } catch (error) { toast.error(errorMessage(error)) }
  }
  const createItemSplit = async (event: FormEvent) => {
    event.preventDefault()
    const lines = Object.entries(itemQuantities).filter(([, quantity]) => quantity > 0).map(([orderItemId, quantity]) => ({ orderItemId, quantity }))
    try {
      const created = await api.post<BillSplitDto>(`/api/order-payments/${orderId}/splits/items`, { name: splitName || null, lines })
      setSplits(current => [...current, created])
      setItemQuantities({})
      setSplitName('')
      setSelectedSplitId(created.id)
      setAmount(created.remainingAmount)
      toast.success(t('orderPayments.splitCreated'))
    } catch (error) { toast.error(errorMessage(error)) }
  }
  const pay = async (event: FormEvent) => {
    event.preventDefault()
    try {
      const path = selectedSplitId
        ? `/api/order-payments/${orderId}/splits/${selectedSplitId}/payments`
        : `/api/order-payments/${orderId}`
      const payment = await api.post<OrderPaymentDto>(path, { paymentMethodCode: method, amount })
      setPayments(current => [...current, payment])
      if (payment.billSplitId) setSplits(current => current.map(split => split.id === payment.billSplitId
        ? { ...split, paidAmount: split.paidAmount + payment.amount, remainingAmount: split.remainingAmount - payment.amount }
        : split))
      setAmount(0)
      setOrders(current => current.map(order => order.id === orderId ? { ...order, status: payment.orderStatus, grandTotal: payment.grandTotal } : order))
      toast.success(t('orderPayments.saved'))
    } catch (error) { toast.error(errorMessage(error)) }
  }
  const edit = async (event: FormEvent) => {
    event.preventDefault()
    try {
      const entry = await api.post<OrderEditLogDto>(`/api/order-payments/${orderId}/edits`, { editType: 'PriceOverride', amountDelta: delta, notes })
      setEdits(current => [entry, ...current])
      setOrders(current => current.map(order => order.id === orderId ? { ...order, grandTotal: entry.orderGrandTotal } : order))
      setDelta(0); setNotes(''); toast.success(t('orderPayments.edited'))
    } catch (error) { toast.error(errorMessage(error)) }
  }
  const allocatedQuantity = (itemId: string) => splits.flatMap(split => split.lines).filter(line => line.orderItemId === itemId).reduce((sum, line) => sum + line.quantity, 0)
  const cannotChangeSplits = !selected || !['Open', 'Sent'].includes(selected.status) || splits.reduce((sum, split) => sum + split.amount, 0) >= selected.grandTotal

  return <section>
    <h1>{t('orderPayments.title')}</h1>
    <div className="table-toolbar">
      <select value={branchId} onChange={event => setBranchId(event.target.value)}>{branches.map(branch => <option key={branch.id} value={branch.id}>{name(branch)}</option>)}</select>
      <select value={orderId} onChange={event => setOrderId(event.target.value)}>{orders.map(order => <option key={order.id} value={order.id}>#{order.orderNumber} · {order.status} · {order.grandTotal.toFixed(3)}</option>)}</select>
    </div>
    {selected && <div className="grid gap-6 lg:grid-cols-2">
      <div className="settings-card">
        <h2>{t('orderPayments.createSplits')}</h2>
        <form onSubmit={createEqual}>
          <label>{t('orderPayments.equalShares')}</label>
          <input required type="number" min="2" max="50" value={shareCount} onChange={event => setShareCount(Number(event.target.value))} />
          <button disabled={cannotChangeSplits}>{t('orderPayments.createEqual')}</button>
        </form>
        <form onSubmit={createItemSplit}>
          <h3>{t('orderPayments.itemSplit')}</h3>
          <input value={splitName} maxLength={100} onChange={event => setSplitName(event.target.value)} placeholder={t('orderPayments.splitName')} />
          {selected.items.filter(item => !item.isCancelled).map(item => {
            const remaining = item.quantity - allocatedQuantity(item.id)
            return <label key={item.id}>{item.name} · {t('orderPayments.quantityRemaining', { count: remaining })}
              <input type="number" min="0" max={remaining} value={itemQuantities[item.id] ?? 0} onChange={event => setItemQuantities(current => ({ ...current, [item.id]: Number(event.target.value) }))} />
            </label>
          })}
          <button disabled={cannotChangeSplits}>{t('orderPayments.createItemSplit')}</button>
        </form>
      </div>
      <form className="settings-card" onSubmit={pay}>
        <h2>{t('orderPayments.payment')}</h2>
        <div>{t('orderPayments.paid')}: <Money value={paid} /> / <Money value={selected.grandTotal} /></div>
        <label>{t('orderPayments.payAgainst')}</label>
        <select value={selectedSplitId} onChange={event => { const id = event.target.value; setSelectedSplitId(id); setAmount(splits.find(split => split.id === id)?.remainingAmount ?? 0) }}>
          <option value="">{t('orderPayments.orderBalance')}</option>
          {splits.map(split => <option key={split.id} value={split.id} disabled={split.remainingAmount <= 0}>{split.name} · {split.remainingAmount.toFixed(3)}</option>)}
        </select>
        {splits.map(split => <div key={split.id}>
          <strong>{split.name}</strong> · {t('orderPayments.remaining')}: <Money value={split.remainingAmount} /> / <Money value={split.amount} />
          {split.lines.length > 0 && <div>{split.lines.map(line => `${line.itemName} × ${line.quantity}`).join(', ')}</div>}
        </div>)}
        <select value={method} onChange={event => setMethod(event.target.value)}>{methods.map(paymentMethod => <option key={paymentMethod.id} value={paymentMethod.code}>{name(paymentMethod)}</option>)}</select>
        <input required type="number" min="0.001" max={selectedSplit?.remainingAmount} step="0.001" value={amount} onChange={event => setAmount(Number(event.target.value))} />
        <button disabled={['Paid', 'Closed'].includes(selected.status) || (selectedSplitId !== '' && !selectedSplit?.remainingAmount)}>{t('orderPayments.pay')}</button>
        {payments.map(payment => <div key={payment.id}>{payment.methodCode} · <Money value={payment.amount} />{payment.billSplitId ? ` · ${splits.find(split => split.id === payment.billSplitId)?.name ?? ''}` : ''}</div>)}
      </form>
      {hasPermission('closedOrders.edit') && <form className="settings-card" onSubmit={edit}>
        <h2>{t('orderPayments.edit')}</h2>
        <input type="number" step="0.001" value={delta} onChange={event => setDelta(Number(event.target.value))} />
        <textarea value={notes} onChange={event => setNotes(event.target.value)} placeholder={t('orderPayments.notes')} />
        <button disabled={!['Paid', 'Closed'].includes(selected.status)}>{t('common.save')}</button>
        {edits.map(entry => <div key={entry.id}>{entry.editType} · <Money value={entry.amountDelta} /> · {entry.notes}</div>)}
      </form>}
    </div>}
  </section>
}
