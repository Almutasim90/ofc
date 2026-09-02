import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, OrderCancellationDto, RestaurantOrderDto } from '../api/types'
import DataTable from '../components/DataTable'
import Money from '../components/Money'
import { useToast } from '../components/ToastContext'

export default function OrderCancellationsPage() {
  const { t } = useTranslation(); const toast = useToast()
  const [branches,setBranches] = useState<BranchDto[]>([]), [branchId,setBranchId] = useState(''), [orders,setOrders] = useState<RestaurantOrderDto[]>([]), [log,setLog] = useState<OrderCancellationDto[]>([])
  const [loading,setLoading] = useState(true)
  useEffect(() => { void api.get<BranchDto[]>('/api/branches').then(x => { setBranches(x); setBranchId(x[0]?.id ?? '') }) }, [])
  const load = useCallback(async () => { if (!branchId) { setLoading(false); return }; setLoading(true); const [o,l] = await Promise.all([api.get<RestaurantOrderDto[]>(`/api/restaurant-orders?branchId=${branchId}`),api.get<OrderCancellationDto[]>(`/api/restaurant-orders/cancellations?branchId=${branchId}`)]); setOrders(o); setLog(l); setLoading(false) }, [branchId])
  useEffect(() => { void load() }, [load])
  const cancel = async (orderId:string,itemId?:string) => { const reason=window.prompt(t('cancellations.reason')); if(!reason)return; try{await api.post(`/api/restaurant-orders/${orderId}${itemId?`/items/${itemId}`:''}/cancel`,{reason});await load();toast.success(t('cancellations.saved'))}catch(e){toast.error(e instanceof ApiError?e.message:t('common.saveError'))} }
  return <section><h1>{t('cancellations.title')}</h1><label>{t('restaurant.branch')}<select value={branchId} onChange={e=>setBranchId(e.target.value)}>{branches.map(x=><option value={x.id} key={x.id}>{x.nameAr} / {x.nameEn}</option>)}</select></label><h2>{t('cancellations.openOrders')}</h2>{orders.filter(x=>!['Paid','Closed','Cancelled'].includes(x.status)).map(o=><div className="settings-card" key={o.id}><div className="table-toolbar"><strong>#{o.orderNumber}</strong><Money value={o.grandTotal}/><button onClick={()=>cancel(o.id)}>{t('cancellations.cancelOrder')}</button></div>{o.items.filter(x=>!x.isCancelled).map(i=><div className="table-toolbar" key={i.id}><span>{i.name} × {i.quantity}</span><Money value={i.lineTotal}/><button onClick={()=>cancel(o.id,i.id)}>{t('cancellations.cancelItem')}</button></div>)}</div>)}<h2>{t('cancellations.log')}</h2>
    <DataTable rows={log} loading={loading} queryPrefix="cancellations" getRowKey={(row) => row.id} defaultSort={{ id: 'date', direction: 'desc' }} getSearchText={(row) => `${row.orderNumber} ${row.itemName ?? ''} ${row.reason}`}
      columns={[
        { id: 'order', header: '#', cell: (row) => row.orderNumber, sortValue: (row) => row.orderNumber },
        { id: 'item', header: t('cancellations.item'), cell: (row) => row.itemName ?? t('cancellations.wholeOrder'), sortValue: (row) => row.itemName ?? '' },
        { id: 'reason', header: t('cancellations.reason'), cell: (row) => row.reason, sortValue: (row) => row.reason },
        { id: 'date', header: t('cancellations.date'), cell: (row) => new Date(row.createdAt).toLocaleString(), sortValue: (row) => new Date(row.createdAt) },
      ]} />
  </section>
}
