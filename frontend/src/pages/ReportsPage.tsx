import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto, DailySalesReportDto, GlobalSalesReportDto, ShiftInventoryReportDto } from '../api/types'

const today = new Date().toISOString().slice(0, 10)
const chartColors = ['#E3A53C', '#2F6E68', '#C0503B', '#A9762A', '#B4A392']

function downloadCsv(filename: string, rows: (string | number)[][]) {
  const escape = (value: string | number) => `"${String(value).replace(/"/g, '""')}"`
  const csv = `\uFEFF${rows.map((row) => row.map(escape).join(',')).join('\r\n')}`
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a'); link.href = url; link.download = filename; link.click(); URL.revokeObjectURL(url)
}

export default function ReportsPage() {
  const { t, i18n } = useTranslation()
  const { user, hasPermission } = useAuth()
  const [date, setDate] = useState(today)
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState(user?.branchId ?? '')
  const [mode, setMode] = useState<'branch' | 'global'>(hasPermission('reports.global.view') ? 'global' : 'branch')
  const [daily, setDaily] = useState<DailySalesReportDto | null>(null)
  const [global, setGlobal] = useState<GlobalSalesReportDto | null>(null)
  const [shiftId, setShiftId] = useState('')
  const [inventory, setInventory] = useState<ShiftInventoryReportDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.get<BranchDto[]>('/api/branches').then((data) => {
      const active = data.filter((branch) => branch.isActive); setBranches(active)
      setBranchId(user?.branchId ?? active[0]?.id ?? '')
    }).catch(() => setError(t('reports.loadError')))
  }, [t, user?.branchId])

  const loadSales = async () => {
    setError(null)
    try {
      if (mode === 'global') {
        setGlobal(await api.get<GlobalSalesReportDto>(`/api/reports/global?date=${date}`)); setDaily(null)
      } else {
        setDaily(await api.get<DailySalesReportDto>(`/api/reports/daily?branchId=${branchId}&date=${date}`)); setGlobal(null)
      }
    } catch { setError(t('reports.loadError')) }
  }

  useEffect(() => { if (mode === 'global' || branchId) loadSales() }, [branchId, date, mode]) // eslint-disable-line react-hooks/exhaustive-deps

  const loadInventory = async (event: FormEvent) => {
    event.preventDefault(); setError(null)
    try { setInventory(await api.get<ShiftInventoryReportDto>(`/api/reports/shifts/${shiftId}/inventory-consumption`)) }
    catch { setError(t('reports.shiftError')) }
  }

  const branchName = (value: { branchNameAr: string; branchNameEn: string }) => i18n.language === 'ar' ? value.branchNameAr : value.branchNameEn
  const materialName = (value: { nameAr: string; nameEn: string }) => i18n.language === 'ar' ? value.nameAr : value.nameEn
  const money = (value: number) => value.toFixed(3)

  const exportSales = () => {
    if (global) downloadCsv(`global-sales-${date}.csv`, [
      [t('reports.branch'), t('reports.totalSales'), t('reports.invoices')],
      ...global.branches.map((row) => [branchName(row), money(row.totalSales), row.invoiceCount]),
    ])
    if (daily) downloadCsv(`branch-sales-${date}.csv`, [
      [t('reports.paymentMethod'), t('reports.totalSales'), t('reports.invoices')],
      ...daily.paymentBreakdown.map((row) => [t(`reports.payment.${row.paymentMethod.toLowerCase()}`), money(row.totalAmount), row.invoiceCount]),
    ])
  }

  const salesChart = global
    ? global.branches.map((row) => ({ name: branchName(row), total: row.totalSales }))
    : daily?.paymentBreakdown.map((row) => ({ name: t(`reports.payment.${row.paymentMethod.toLowerCase()}`), total: row.totalAmount })) ?? []

  return (
    <section>
      <div className="flex flex-wrap items-center justify-between gap-3"><h1>{t('reports.title')}</h1>{(daily || global) && <button onClick={exportSales}>{t('reports.exportCsv')}</button>}</div>
      {error && <p className="error-text">{error}</p>}
      <div className="mt-4 flex flex-wrap gap-3 rounded-xl border border-border bg-surface p-4">
        {hasPermission('reports.global.view') && <label className="flex flex-col gap-1 text-muted">{t('reports.scope')}<select value={mode} onChange={(e) => setMode(e.target.value as 'branch' | 'global')}><option value="global">{t('reports.global')}</option><option value="branch">{t('reports.branch')}</option></select></label>}
        {mode === 'branch' && !user?.branchId && <label className="flex flex-col gap-1 text-muted">{t('reports.branch')}<select value={branchId} onChange={(e) => setBranchId(e.target.value)}>{branches.map((b) => <option key={b.id} value={b.id}>{i18n.language === 'ar' ? b.nameAr : b.nameEn}</option>)}</select></label>}
        <label className="flex flex-col gap-1 text-muted">{t('reports.date')}<input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label>
      </div>

      {(daily || global) && <>
        <div className="mt-4 grid gap-4 sm:grid-cols-2"><div className="rounded-xl border border-border bg-surface p-5"><span className="text-muted">{t('reports.totalSales')}</span><strong className="block font-cairo text-3xl text-primary">{money((daily ?? global)!.totalSales)}</strong></div><div className="rounded-xl border border-border bg-surface p-5"><span className="text-muted">{t('reports.invoices')}</span><strong className="block font-cairo text-3xl">{(daily ?? global)!.invoiceCount}</strong></div></div>
        <div className="mt-4 h-80 rounded-xl border border-border bg-surface p-4"><ResponsiveContainer width="100%" height="100%"><BarChart data={salesChart}><CartesianGrid strokeDasharray="3 3" stroke="#2B2019"/><XAxis dataKey="name"/><YAxis/><Tooltip/><Bar dataKey="total" fill="#E3A53C" radius={[6,6,0,0]}/></BarChart></ResponsiveContainer></div>
        <table><thead><tr><th>{global ? t('reports.branch') : t('reports.paymentMethod')}</th><th>{t('reports.totalSales')}</th><th>{t('reports.invoices')}</th></tr></thead><tbody>
          {global?.branches.map((row) => <tr key={row.branchId}><td>{branchName(row)}</td><td>{money(row.totalSales)}</td><td>{row.invoiceCount}</td></tr>)}
          {daily?.paymentBreakdown.map((row) => <tr key={row.paymentMethod}><td>{t(`reports.payment.${row.paymentMethod.toLowerCase()}`)}</td><td>{money(row.totalAmount)}</td><td>{row.invoiceCount}</td></tr>)}
        </tbody></table>
      </>}

      <form className="mt-8 rounded-xl border border-border bg-surface p-4" onSubmit={loadInventory}><h2>{t('reports.inventoryTitle')}</h2><div className="mt-3 flex flex-wrap items-end gap-3"><label className="flex flex-1 flex-col gap-1 text-muted">{t('reports.shiftId')}<input required value={shiftId} onChange={(e) => setShiftId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" /></label><button>{t('reports.load')}</button></div></form>
      {inventory && <div className="mt-4 grid gap-4 lg:grid-cols-2"><div className="h-72 rounded-xl border border-border bg-surface p-4"><ResponsiveContainer><PieChart><Pie data={inventory.materials.map((row) => ({ name: materialName(row), value: row.quantityConsumed }))} dataKey="value" nameKey="name" outerRadius={95}>{inventory.materials.map((row, index) => <Cell key={row.rawMaterialId} fill={chartColors[index % chartColors.length]}/>)}</Pie><Tooltip/></PieChart></ResponsiveContainer></div><table><thead><tr><th>{t('reports.material')}</th><th>{t('reports.consumed')}</th><th>{t('reports.unit')}</th></tr></thead><tbody>{inventory.materials.map((row) => <tr key={row.rawMaterialId}><td>{materialName(row)}</td><td>{row.quantityConsumed.toFixed(3)}</td><td>{row.unit}</td></tr>)}</tbody></table></div>}
    </section>
  )
}
