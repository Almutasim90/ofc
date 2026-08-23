import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, ShiftDto } from '../api/types'

export default function ShiftPage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const [shift, setShift] = useState<ShiftDto | null>(null)
  const [lastClosed, setLastClosed] = useState<ShiftDto | null>(null)
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState(user?.branchId ?? '')
  const [openingCash, setOpeningCash] = useState('0')
  const [closingCashActual, setClosingCashActual] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const load = async () => {
      try {
        const [current, latestClosed, branchData] = await Promise.all([
          api.get<ShiftDto | undefined>('/api/shifts/current'),
          api.get<ShiftDto | undefined>('/api/shifts/latest-closed'),
          api.get<BranchDto[]>('/api/branches'),
        ])
        setShift(current ?? null)
        setLastClosed(latestClosed ?? null)
        const active = branchData.filter((branch) => branch.isActive)
        setBranches(active)
        setBranchId(user?.branchId ?? active[0]?.id ?? '')
      } catch {
        setError(t('shifts.loadError'))
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [t, user?.branchId])

  const openShift = async (event: FormEvent) => {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      const opened = await api.post<ShiftDto>('/api/shifts/open', { branchId, openingCash: Number(openingCash) })
      setShift(opened)
      setLastClosed(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('shifts.openError'))
    } finally {
      setSubmitting(false)
    }
  }

  const closeShift = async (event: FormEvent) => {
    event.preventDefault()
    if (!shift) return
    setSubmitting(true)
    setError(null)
    try {
      const closed = await api.post<ShiftDto>(`/api/shifts/${shift.id}/close`, {
        closingCashActual: Number(closingCashActual),
      })
      setLastClosed(closed)
      setShift(null)
      setClosingCashActual('')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('shifts.closeError'))
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <p>{t('common.loading')}</p>
  const branchName = (branch: BranchDto) => i18n.language === 'ar' ? branch.nameAr : branch.nameEn
  const money = (value: number) => value.toFixed(3)

  return (
    <section>
      <h1>{t('shifts.title')}</h1>
      {error && <p className="error-text">{error}</p>}

      {shift ? (
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <div className="rounded-xl border border-border bg-surface p-5">
            <h2>{t('shifts.current')}</h2>
            <dl className="mt-3 space-y-2">
              <div className="flex justify-between"><dt>{t('shifts.openingCash')}</dt><dd>{money(shift.openingCash)}</dd></div>
              <div className="flex justify-between"><dt>{t('shifts.cashSales')}</dt><dd>{money(shift.cashSalesTotal)}</dd></div>
              <div className="flex justify-between font-bold"><dt>{t('shifts.expected')}</dt><dd>{money(shift.closingCashExpected)}</dd></div>
            </dl>
          </div>
          <form className="rounded-xl border border-border bg-surface p-5" onSubmit={closeShift}>
            <h2>{t('shifts.close')}</h2>
            <label className="mt-3 flex flex-col gap-1 text-muted">
              {t('shifts.actual')}
              <input type="number" min="0" step="0.001" required value={closingCashActual}
                onChange={(event) => setClosingCashActual(event.target.value)} />
            </label>
            <button className="mt-4" disabled={submitting}>{t('shifts.closeSubmit')}</button>
          </form>
        </div>
      ) : (
        <form className="mt-4 max-w-md rounded-xl border border-border bg-surface p-5" onSubmit={openShift}>
          <h2>{t('shifts.open')}</h2>
          {!user?.branchId && (
            <label className="mt-3 flex flex-col gap-1 text-muted">
              {t('shifts.branch')}
              <select required value={branchId} onChange={(event) => setBranchId(event.target.value)}>
                {branches.map((branch) => <option key={branch.id} value={branch.id}>{branchName(branch)}</option>)}
              </select>
            </label>
          )}
          <label className="mt-3 flex flex-col gap-1 text-muted">
            {t('shifts.openingCash')}
            <input type="number" min="0" step="0.001" required value={openingCash}
              onChange={(event) => setOpeningCash(event.target.value)} />
          </label>
          <button className="mt-4" disabled={submitting || !branchId}>{t('shifts.openSubmit')}</button>
        </form>
      )}

      {lastClosed && (
        <div className="mt-6 rounded-xl border border-border bg-surface p-5">
          <h2>{t('shifts.summary')}</h2>
          <div className="mt-3 grid gap-3 sm:grid-cols-3">
            <div><span className="text-muted">{t('shifts.expected')}</span><strong className="block">{money(lastClosed.closingCashExpected)}</strong></div>
            <div><span className="text-muted">{t('shifts.actual')}</span><strong className="block">{money(lastClosed.closingCashActual ?? 0)}</strong></div>
            <div><span className="text-muted">{t('shifts.variance')}</span><strong className={`block ${(lastClosed.varianceAmount ?? 0) < 0 ? 'text-danger' : 'text-primary'}`}>{money(lastClosed.varianceAmount ?? 0)}</strong></div>
          </div>
        </div>
      )}
    </section>
  )
}
