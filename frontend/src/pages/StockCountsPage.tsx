import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import type { BranchDto, StockCountDto, WarehouseDto } from '../api/types'
import { useToast } from '../components/ToastContext'

export default function StockCountsPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([])
  const [warehouseId, setWarehouseId] = useState('')
  const [count, setCount] = useState<StockCountDto | null>(null)

  useEffect(() => {
    void api.get<BranchDto[]>('/api/branches').then((value) => {
      setBranches(value)
      setBranchId(value[0]?.id ?? '')
    })
  }, [])
  useEffect(() => {
    if (!branchId) return
    void api.get<WarehouseDto[]>(`/api/restaurant-inventory/warehouses?branchId=${branchId}`).then((value) => {
      const active = value.filter((warehouse) => warehouse.isActive)
      setWarehouses(active)
      setWarehouseId(active[0]?.id ?? '')
      setCount(null)
    })
  }, [branchId])

  const start = async () => {
    if (!warehouseId) return toast.error(t('stockCounts.warehouseRequired'))
    try {
      setCount(await api.post<StockCountDto>('/api/stock-counts', { branchId, warehouseId }))
    } catch (error) { toast.error(error instanceof ApiError ? error.message : t('common.saveError')) }
  }
  const save = async () => {
    if (!count) return
    try {
      await api.put(`/api/stock-counts/${count.id}`, count.lines.map((line) => ({ ingredientId: line.ingredientId, countedQuantity: line.countedQuantity })))
      toast.success(t('stockCounts.saved'))
    } catch (error) { toast.error(error instanceof ApiError ? error.message : t('common.saveError')) }
  }
  const finalize = async () => {
    if (!count || !confirm(t('stockCounts.confirmFinalize'))) return
    try {
      await api.put(`/api/stock-counts/${count.id}`, count.lines.map((line) => ({ ingredientId: line.ingredientId, countedQuantity: line.countedQuantity })))
      await api.post(`/api/stock-counts/${count.id}/finalize`, {})
      setCount(await api.get<StockCountDto>(`/api/stock-counts/${count.id}`))
      toast.success(t('stockCounts.finalizedMessage'))
    } catch (error) { toast.error(error instanceof ApiError ? error.message : t('common.saveError')) }
  }

  return <section>
    <h1>{t('stockCounts.title')}</h1>
    <div className="table-toolbar">
      <select value={branchId} onChange={(event) => setBranchId(event.target.value)}>{branches.map((branch) => <option key={branch.id} value={branch.id}>{i18n.language === 'ar' ? branch.nameAr : branch.nameEn}</option>)}</select>
      <select value={warehouseId} onChange={(event) => { setWarehouseId(event.target.value); setCount(null) }}>{warehouses.map((warehouse) => <option key={warehouse.id} value={warehouse.id}>{i18n.language === 'ar' ? warehouse.nameAr : warehouse.nameEn}</option>)}</select>
      <button disabled={!warehouseId || count?.status === 'Draft'} onClick={() => void start()}>{t('stockCounts.start')}</button>
    </div>
    {count && <div className="settings-card">
      <div className="table-toolbar"><strong>{t(`stockCounts.${count.status.toLowerCase()}`)}</strong><span>{new Date(count.createdAt).toLocaleString()}</span></div>
      {count.lines.map((line, index) => <div className="table-toolbar" key={line.ingredientId}>
        <span>{line.name}</span><span>{t('stockCounts.system')}: {line.systemQuantity}</span>
        <input type="number" min="0" step="0.001" disabled={count.status !== 'Draft'} value={line.countedQuantity} aria-label={`${t('stockCounts.counted')} ${line.name}`} onChange={(event) => setCount((value) => value && ({ ...value, lines: value.lines.map((item, itemIndex) => itemIndex === index ? { ...item, countedQuantity: Number(event.target.value), varianceQuantity: Number(event.target.value) - item.systemQuantity } : item) }))} />
        <strong>{t('stockCounts.variance')}: {line.varianceQuantity}</strong>
      </div>)}
      {count.status === 'Draft' && <div className="modal-actions"><button onClick={() => void save()}>{t('common.save')}</button><button onClick={() => void finalize()}>{t('stockCounts.finalize')}</button></div>}
    </div>}
  </section>
}
