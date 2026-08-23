import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, StockStatusDto } from '../api/types'

export default function InventoryPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState('')
  const [stock, setStock] = useState<StockStatusDto[]>([])
  const [loading, setLoading] = useState(true)
  const [adjusting, setAdjusting] = useState<StockStatusDto | null>(null)

  useEffect(() => {
    const init = async () => {
      const branchesData = await api.get<BranchDto[]>('/api/branches')
      setBranches(branchesData)
      setBranchId(user?.branchId ?? branchesData[0]?.id ?? '')
    }
    init()
  }, [user])

  const load = async () => {
    if (!branchId) return
    setLoading(true)
    setStock(await api.get<StockStatusDto[]>(`/api/inventory/stock?branchId=${branchId}`))
    setLoading(false)
  }

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId])

  return (
    <div>
      <h1>{t('inventory.title')}</h1>
      <label>
        {t('inventory.branch')}
        <select value={branchId} onChange={(e) => setBranchId(e.target.value)} disabled={!!user?.branchId}>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.nameEn}
            </option>
          ))}
        </select>
      </label>

      {loading ? (
        <p>{t('common.loading')}</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>{t('inventory.rawMaterial')}</th>
              <th>{t('inventory.currentQuantity')}</th>
              <th>{t('inventory.lowStockThreshold')}</th>
              <th></th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {stock.map((item) => (
              <tr key={item.rawMaterialId}>
                <td>
                  {item.nameEn} ({item.unit})
                </td>
                <td>{item.currentQuantity}</td>
                <td>{item.lowStockThreshold}</td>
                <td>{item.isLowStock && <span className="error-text">{t('inventory.lowStock')}</span>}</td>
                <td>
                  <button type="button" onClick={() => setAdjusting(item)}>
                    {t('inventory.adjust')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {adjusting && (
        <AdjustModal
          item={adjusting}
          branchId={branchId}
          onClose={() => setAdjusting(null)}
          onSaved={async () => {
            setAdjusting(null)
            await load()
          }}
        />
      )}
    </div>
  )
}

function AdjustModal({
  item,
  branchId,
  onClose,
  onSaved,
}: {
  item: StockStatusDto
  branchId: string
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const [quantityChange, setQuantityChange] = useState('0')
  const [reason, setReason] = useState('')
  const [threshold, setThreshold] = useState(item.lowStockThreshold.toString())
  const [submitting, setSubmitting] = useState(false)

  const submitAdjustment = async () => {
    setSubmitting(true)
    try {
      await api.post('/api/inventory/stock-adjustments', {
        branchId,
        rawMaterialId: item.rawMaterialId,
        quantityChange: Number(quantityChange),
        reason,
      })
      onSaved()
    } finally {
      setSubmitting(false)
    }
  }

  const submitThreshold = async () => {
    setSubmitting(true)
    try {
      await api.put('/api/inventory/stock/low-stock-threshold', {
        branchId,
        rawMaterialId: item.rawMaterialId,
        threshold: Number(threshold),
      })
      onSaved()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <h2>{t('inventory.adjustTitle')}</h2>
        <p>
          {item.nameEn} ({item.unit})
        </p>
        <label>
          {t('inventory.quantityChange')}
          <input type="number" step="0.001" value={quantityChange} onChange={(e) => setQuantityChange(e.target.value)} />
          <small>{t('inventory.quantityChangeHint')}</small>
        </label>
        <label>
          {t('inventory.reason')}
          <input value={reason} onChange={(e) => setReason(e.target.value)} required />
        </label>
        <div className="modal-actions">
          <button type="button" onClick={submitAdjustment} disabled={submitting || !reason}>
            {t('inventory.save')}
          </button>
        </div>

        <hr />

        <label>
          {t('inventory.lowStockThreshold')}
          <input type="number" step="0.001" min="0" value={threshold} onChange={(e) => setThreshold(e.target.value)} />
        </label>
        <div className="modal-actions">
          <button type="button" onClick={submitThreshold} disabled={submitting}>
            {t('inventory.setThreshold')}
          </button>
          <button type="button" onClick={onClose}>
            {t('inventory.cancel')}
          </button>
        </div>
      </div>
    </div>
  )
}
