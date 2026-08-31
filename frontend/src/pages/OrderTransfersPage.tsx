import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, OrderingPointDto, RestaurantOrderDto } from '../api/types'
import { useToast } from '../components/ToastContext'

export default function OrderTransfersPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [orders, setOrders] = useState<RestaurantOrderDto[]>([])
  const [points, setPoints] = useState<OrderingPointDto[]>([])
  const [targets, setTargets] = useState<Record<string, string>>({})
  const [notes, setNotes] = useState<Record<string, string>>({})

  useEffect(() => {
    void api.get<BranchDto[]>('/api/branches').then((value) => {
      setBranches(value)
      setBranchId(value[0]?.id ?? '')
    })
  }, [])

  const load = useCallback(async () => {
    if (!branchId) return
    const [loadedOrders, loadedPoints] = await Promise.all([
      api.get<RestaurantOrderDto[]>(`/api/restaurant-orders?branchId=${branchId}`),
      api.get<OrderingPointDto[]>(`/api/ordering-points?branchId=${branchId}`),
    ])
    setOrders(loadedOrders.filter((order) => order.status !== 'Paid' && order.status !== 'Closed'))
    setPoints(loadedPoints.filter((point) => point.isActive))
  }, [branchId])

  useEffect(() => { void load() }, [load])

  const transfer = async (orderId: string) => {
    const newOrderingPointId = targets[orderId]
    if (!newOrderingPointId) return toast.error(t('orderTransfers.targetRequired'))
    try {
      await api.post(`/api/orders/${orderId}/transfer`, { newOrderingPointId, notes: notes[orderId] || null })
      toast.success(t('orderTransfers.transferred'))
      await load()
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : t('common.saveError'))
    }
  }

  return <section>
    <h1>{t('orderTransfers.title')}</h1>
    <div className="table-toolbar">
      <select value={branchId} onChange={(event) => setBranchId(event.target.value)}>
        {branches.map((branch) => <option key={branch.id} value={branch.id}>{i18n.language === 'ar' ? branch.nameAr : branch.nameEn}</option>)}
      </select>
    </div>
    <div className="grid gap-3">
      {orders.map((order) => <article className="settings-card" key={order.id}>
        <strong>#{order.orderNumber} · {order.status}{order.tableLabel ? ` · ${order.tableLabel}` : ''}</strong>
        <div className="table-toolbar">
          <select value={targets[order.id] ?? ''} onChange={(event) => setTargets((value) => ({ ...value, [order.id]: event.target.value }))}>
            <option value="">{t('orderTransfers.selectTarget')}</option>
            {points.map((point) => <option key={point.id} value={point.id}>{point.pointType} · {point.label}</option>)}
          </select>
          <input value={notes[order.id] ?? ''} onChange={(event) => setNotes((value) => ({ ...value, [order.id]: event.target.value }))} placeholder={t('orderTransfers.notes')} />
          <button onClick={() => void transfer(order.id)}>{t('orderTransfers.transfer')}</button>
        </div>
      </article>)}
      {!orders.length && <div className="empty-state">{t('orderTransfers.empty')}</div>}
    </div>
  </section>
}
