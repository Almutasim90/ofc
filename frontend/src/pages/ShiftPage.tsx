import { useEffect, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, SaleDto, ShiftDto } from '../api/types'
import Money from '../components/Money'
import AppIcon from '../components/AppIcon'
import Receipt from '../components/Receipt'
import DataTable from '../components/DataTable'
import { IconAction } from '../components/TableTools'
import { useToast } from '../components/ToastContext'

export default function ShiftPage() {
  const { t, i18n } = useTranslation()
  const { user, hasPermission } = useAuth()
  const toast = useToast()
  const [shift, setShift] = useState<ShiftDto | null>(null)
  const [lastClosed, setLastClosed] = useState<ShiftDto | null>(null)
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState(user?.branchId ?? '')
  const [openingCash, setOpeningCash] = useState('0')
  const [customizeOpening, setCustomizeOpening] = useState(false)
  const denominations = [50, 20, 10, 5, 1, 0.5, 0.1, 0.05]
  const [cashCounts, setCashCounts] = useState<Record<string, number>>(() => Object.fromEntries(denominations.map((value) => [value.toString(), 0])))
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [sales, setSales] = useState<SaleDto[]>([])
  const [receiptHeader, setReceiptHeader] = useState<string | null>(null)
  const [printSale, setPrintSale] = useState<SaleDto | null>(null)

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
        const selectedBranchId = user?.branchId ?? active[0]?.id ?? ''
        setBranchId(selectedBranchId)
        setOpeningCash((active.find((branch) => branch.id === selectedBranchId)?.defaultOpeningFloat ?? 0).toString())
      } catch {
        setError(t('shifts.loadError'))
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [t, user?.branchId])

  useEffect(() => {
    api.get<{ headerText: string | null }>('/api/receipt-settings').then((x) => setReceiptHeader(x.headerText)).catch(() => {})
  }, [])

  useEffect(() => {
    if (!shift) { setSales([]); return }
    api.get<SaleDto[]>(`/api/sales?shiftId=${shift.id}`).then(setSales).catch(() => {})
  }, [shift])

  useEffect(() => {
    if (!printSale) return
    window.print()
    const reset = () => setPrintSale(null)
    window.addEventListener('afterprint', reset)
    return () => window.removeEventListener('afterprint', reset)
  }, [printSale])

  const openShift = async (event: FormEvent) => {
    event.preventDefault()
    setSubmitting(true)
    try {
      const opened = await api.post<ShiftDto>('/api/shifts/open', { branchId, openingCash: customizeOpening ? Number(openingCash) : null })
      setShift(opened)
      setLastClosed(null)
      toast.success(t('shifts.opened'))
    } catch (err) {
      toast.error(err instanceof ApiError && (err.status === 401 || err.status === 403) ? t('shifts.sessionExpired') : t('shifts.openError'))
    } finally {
      setSubmitting(false)
    }
  }

  const closeShift = async (event: FormEvent) => {
    event.preventDefault()
    if (!shift) return
    setSubmitting(true)
    try {
      const closed = await api.post<ShiftDto>(`/api/shifts/${shift.id}/close`, {
        counts: denominations.map((denomination) => ({ denomination, quantity: cashCounts[denomination.toString()] ?? 0 })),
      })
      setLastClosed(closed)
      setShift(null)
      setCashCounts(Object.fromEntries(denominations.map((value) => [value.toString(), 0])))
      toast.success(t('shifts.closed'))
    } catch (err) {
      const message = err instanceof ApiError && (err.status === 401 || err.status === 403) ? t('shifts.sessionExpired') : t('shifts.closeError')
      // The same shift may have been closed from another device. Refresh the
      // state so a desktop left open does not keep showing a stale close form.
      try {
        const [current, latestClosed] = await Promise.all([
          api.get<ShiftDto | undefined>('/api/shifts/current'),
          api.get<ShiftDto | undefined>('/api/shifts/latest-closed'),
        ])
        setShift(current ?? null)
        setLastClosed(latestClosed ?? null)
        if (!current) toast.success(t('shifts.closed'))
        else toast.error(message)
      } catch {
        toast.error(message)
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <p>{t('common.loading')}</p>
  const branchName = (branch: BranchDto) => i18n.language === 'ar' ? branch.nameAr : branch.nameEn
  const selectedBranch = branches.find((branch) => branch.id === branchId)
  const printSaleBranch = branches.find((branch) => branch.id === printSale?.branchId)
  const printBranchName = printSaleBranch ? branchName(printSaleBranch) : ''
  const paymentLabel = (method: string) => t(`cashier.${method.toLowerCase()}`)
  const countedTotal = denominations.reduce((sum, denomination) => sum + denomination * (cashCounts[denomination.toString()] ?? 0), 0)
  const changeCount = (denomination: number, delta: number) => setCashCounts((current) => ({
    ...current,
    [denomination.toString()]: Math.max(0, (current[denomination.toString()] ?? 0) + delta),
  }))
  return (
    <section>
      <h1>{t('shifts.title')}</h1>
      {error && <p className="error-text">{error}</p>}

      {shift ? (
        <div className="grid gap-4 md:grid-cols-2">
          <div className="ui-card ui-stack">
            <h2>{t('shifts.current')}</h2>
            <dl className="grid gap-2">
              <div className="flex justify-between"><dt>{t('shifts.openingCash')}</dt><dd><Money value={shift.openingCash} /></dd></div>
              <div className="flex justify-between"><dt>{t('shifts.cashSales')}</dt><dd><Money value={shift.cashSalesTotal} /></dd></div>
              <div className="flex justify-between font-bold"><dt>{t('shifts.expected')}</dt><dd><Money value={shift.closingCashExpected} /></dd></div>
            </dl>
          </div>
          <form className="ui-card ui-stack" onSubmit={closeShift} aria-busy={submitting}>
            <h2>{t('shifts.close')}</h2>
            <p className="text-sm text-muted">{t('shifts.cashCountHint')}</p>
            <div className="cash-count-grid">
              {denominations.map((denomination) => <div className="cash-count-row" key={denomination}>
                <strong><Money value={denomination} /></strong>
                <div className="cash-count-controls">
                  <button type="button" onClick={() => changeCount(denomination, -1)} disabled={!cashCounts[denomination.toString()]} aria-label={t('shifts.decreaseQuantity')}><MinusIcon /></button>
                  <input
                    className="cash-count-input"
                    type="number"
                    min="0"
                    step="1"
                    inputMode="numeric"
                    value={cashCounts[denomination.toString()] ?? 0}
                    onChange={(event) => {
                      const quantity = Math.max(0, Math.floor(Number(event.target.value) || 0))
                      setCashCounts((current) => ({ ...current, [denomination.toString()]: quantity }))
                    }}
                    aria-label={t('shifts.denominationQuantity', { denomination })}
                  />
                  <button type="button" onClick={() => changeCount(denomination, 1)} aria-label={t('shifts.increaseQuantity')}><PlusIcon /></button>
                </div>
              </div>)}
            </div>
            <div className="shift-close-actions">
              <div className="cash-count-total"><span>{t('shifts.countedTotal')}</span><Money value={countedTotal} /></div>
              <button type="submit" disabled={submitting}>
                {submitting ? t('shifts.closingSubmit') : t('shifts.closeSubmit')}
              </button>
            </div>
          </form>
        </div>
      ) : (
        <form className="ui-card ui-stack max-w-md" onSubmit={openShift}>
          <h2>{t('shifts.open')}</h2>
          {!user?.branchId && branches.length === 0 ? (
            <p className="error-text" role="alert">
              {hasPermission('branches.manage') ? (
                <>
                  {t('shifts.noBranchesAdmin')} <Link to="/branches">{t('nav.branches')}</Link>
                </>
              ) : (
                t('shifts.noBranches')
              )}
            </p>
          ) : (
            <>
              {!user?.branchId && (
                <label className="flex flex-col gap-1 text-muted">
                  {t('shifts.branch')}
                  <select required value={branchId} onChange={(event) => setBranchId(event.target.value)}>
                    {branches.map((branch) => <option key={branch.id} value={branch.id}>{branchName(branch)}</option>)}
                  </select>
                </label>
              )}
              <div className="shift-opening-float">
                <span>{t('shifts.defaultOpening')}</span>
                <strong><Money value={customizeOpening ? Number(openingCash) : selectedBranch?.defaultOpeningFloat ?? 0} /></strong>
              </div>
              <button type="button" className="shift-edit-opening" onClick={() => setCustomizeOpening((value) => !value)}>{customizeOpening ? t('shifts.useDefault') : t('shifts.editOpening')}</button>
              {customizeOpening && <label className="flex flex-col gap-1 text-muted">
                  {t('shifts.openingCash')}
                  <input type="number" min="0" step="0.001" required value={openingCash} onChange={(event) => setOpeningCash(event.target.value)} />
              </label>}
              <button disabled={submitting || !branchId}>{t('shifts.openSubmit')}</button>
            </>
          )}
        </form>
      )}

      {shift && (
        <div className="ui-card ui-stack">
          <h2>{t('shifts.salesTitle')}</h2>
          <DataTable rows={sales} getRowKey={(sale) => sale.id} queryPrefix="sales" emptyMessage={t('shifts.salesEmpty')}
            defaultSort={{ id: 'time', direction: 'desc' }}
            getSearchText={(sale) => `${sale.saleNumber} ${paymentLabel(sale.paymentMethod)} ${sale.items.map((item) => item.productNameSnapshot).join(' ')}`}
            columns={[
              { id: 'number', header: '#', cell: (sale) => `#${sale.saleNumber}`, sortValue: (sale) => sale.saleNumber },
              { id: 'time', header: t('shifts.time'), cell: (sale) => new Date(sale.createdAt).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' }), sortValue: (sale) => new Date(sale.createdAt) },
              { id: 'payment', header: t('cashier.paymentMethod'), cell: (sale) => paymentLabel(sale.paymentMethod), sortValue: (sale) => paymentLabel(sale.paymentMethod) },
              { id: 'total', header: t('cashier.total'), cell: (sale) => <Money value={sale.totalAmount} />, sortValue: (sale) => sale.totalAmount },
              { id: 'actions', header: t('branches.actions'), cell: (sale) => <IconAction label={t('receipt.print')} onClick={() => setPrintSale(sale)}><AppIcon className="h-4 w-4" name="printer" /></IconAction> },
            ]} />
        </div>
      )}

      {printSale && createPortal(
        <Receipt sale={printSale} headerText={receiptHeader} branchName={printBranchName} cashierName={user?.fullName ?? ''} />,
        document.body,
      )}

      {lastClosed && (
        <div className="ui-card ui-stack">
          <h2>{t('shifts.summary')}</h2>
          <div className="grid gap-3 sm:grid-cols-3">
            <div><span className="text-muted">{t('shifts.expected')}</span><strong className="block"><Money value={lastClosed.closingCashExpected} /></strong></div>
            <div><span className="text-muted">{t('shifts.actual')}</span><strong className="block"><Money value={lastClosed.closingCashActual ?? 0} /></strong></div>
            <div><span className="text-muted">{t('shifts.variance')}</span><strong className={`block ${(lastClosed.varianceAmount ?? 0) < 0 ? 'text-danger' : 'text-primary'}`}><Money value={lastClosed.varianceAmount ?? 0} /></strong></div>
          </div>
        </div>
      )}
    </section>
  )
}

function MinusIcon() {
  return <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round"><path d="M5 10h10" /></svg>
}

function PlusIcon() {
  return <svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round"><path d="M5 10h10M10 5v10" /></svg>
}
