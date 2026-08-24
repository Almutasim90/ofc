import { useEffect, useMemo, useState, type ReactNode, type SVGProps } from 'react'
import { useTranslation } from 'react-i18next'
import { Area, Bar, BarChart, CartesianGrid, Cell, ComposedChart, LabelList, Line, Pie, PieChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis, type PieLabelRenderProps } from 'recharts'
import { api } from '../api/client'
import type { BranchDto, ChannelSalesDto, ManagerDashboardDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import Money from '../components/Money'
import { SearchBox } from '../components/TableTools'

const formatLocalDate = (date: Date) => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const isoToday = formatLocalDate(new Date())
const isoMonthStart = (() => {
  const today = new Date()
  return formatLocalDate(new Date(today.getFullYear(), today.getMonth(), 1))
})()
const CHART_1 = 'rgb(var(--chart-1))'
const CHART_2 = 'rgb(var(--chart-2))'
const CHART_3 = 'rgb(var(--chart-3))'
const CHART_4 = 'rgb(var(--chart-4))'
const CHART_5 = 'rgb(var(--chart-5))'
const CHART_6 = 'rgb(var(--chart-6))'
const CHANNEL_COLORS = [CHART_1, CHART_2, CHART_3, CHART_4, CHART_5, CHART_6]
const MUTED = 'rgb(var(--color-muted))'
const BORDER = 'rgb(var(--color-border))'
const PRIMARY = 'rgb(var(--color-primary))'
const DANGER = 'rgb(var(--color-danger))'
type ProductSortKey = 'product' | 'quantitySold' | 'totalSales' | 'invoiceCount' | 'share'
type SortDirection = 'asc' | 'desc'

const tooltipBase = {
  contentStyle: { borderRadius: 12, border: `1px solid ${BORDER}`, background: 'rgb(var(--color-surface))', color: 'rgb(var(--color-text))', fontSize: 14, padding: '0.5rem 0.75rem', boxShadow: 'var(--shadow-card-hover)' },
  labelStyle: { color: MUTED, marginBottom: 4, fontWeight: 600 },
  itemStyle: { color: 'rgb(var(--color-text))' },
}

export default function ReportsPage() {
  const { t, i18n } = useTranslation()
  const { user, hasPermission } = useAuth()
  const [from, setFrom] = useState(isoMonthStart)
  const [to, setTo] = useState(isoToday)
  const [branchId, setBranchId] = useState(user?.branchId ?? '')
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [data, setData] = useState<ManagerDashboardDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [productSearch, setProductSearch] = useState('')
  const [channelSales, setChannelSales] = useState<ChannelSalesDto[]>([])
  const [lowStockCount, setLowStockCount] = useState(0)
  const [showTable, setShowTable] = useState(false)
  const [productSort, setProductSort] = useState<{ key: ProductSortKey; direction: SortDirection }>({ key: 'totalSales', direction: 'desc' })

  useEffect(() => { api.get<BranchDto[]>('/api/branches').then((rows) => setBranches(rows.filter((b) => b.isActive))).catch(() => {}) }, [])
  useEffect(() => {
    setLoading(true); setError(null)
    const queryBranch = user?.branchId ?? branchId
    api.get<ManagerDashboardDto>(`/api/reports/dashboard?from=${from}&to=${to}${queryBranch ? `&branchId=${queryBranch}` : ''}`)
      .then(setData).catch(() => setError(t('reports.loadError'))).finally(() => setLoading(false))
  }, [branchId, from, to, t, user?.branchId])
  useEffect(()=>{const suffix=(user?.branchId??branchId)?`&branchId=${user?.branchId??branchId}`:'';api.get<ChannelSalesDto[]>(`/api/reports/channels?from=${from}&to=${to}${suffix}`).then(setChannelSales).catch(()=>{});if(hasPermission('inventory.adjust'))api.get<unknown[]>('/api/notifications/low-stock').then(x=>setLowStockCount(x.length)).catch(()=>{})},[branchId,from,to,user?.branchId,hasPermission])

  const name = (ar: string, en: string) => i18n.language === 'ar' ? ar : en
  const trend = useMemo(() => data?.dailyTrend.map((x) => ({ ...x, averageTicket: x.invoiceCount ? x.totalSales / x.invoiceCount : 0, label: new Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'short' }).format(new Date(`${x.date}T12:00:00`)) })) ?? [], [data, i18n.language])
  const products = data?.products ?? []
  const filteredProducts = products.filter((x) => `${x.nameAr} ${x.nameEn}`.toLocaleLowerCase().includes(productSearch.trim().toLocaleLowerCase()))
  const sortedProducts = useMemo(() => [...filteredProducts].sort((a, b) => {
    const multiplier = productSort.direction === 'asc' ? 1 : -1
    if (productSort.key === 'product') return multiplier * name(a.nameAr, a.nameEn).localeCompare(name(b.nameAr, b.nameEn), i18n.language)
    const aValue = productSort.key === 'share' ? a.totalSales : a[productSort.key]
    const bValue = productSort.key === 'share' ? b.totalSales : b[productSort.key]
    return multiplier * (aValue - bValue)
  }), [filteredProducts, productSort, i18n.language])
  const toggleProductSort = (key: ProductSortKey) => setProductSort((current) => ({
    key,
    direction: current.key === key && current.direction === 'desc' ? 'asc' : 'desc',
  }))
  const isAr = i18n.language === 'ar'
  const topProducts = useMemo(() => (data?.products ?? []).map((x) => ({ ...x, name: isAr ? x.nameAr : x.nameEn })).sort((a, b) => b.quantitySold - a.quantitySold).slice(0, 6), [data, isAr])
  const topRevenueProducts = useMemo(() => (data?.products ?? []).map((x) => ({ ...x, name: isAr ? x.nameAr : x.nameEn })).sort((a, b) => b.totalSales - a.totalSales).slice(0, 6), [data, isAr])
  const branchData = useMemo(() => (data?.branches ?? []).map((x) => ({ ...x, name: isAr ? x.branchNameAr : x.branchNameEn })).sort((a, b) => b.totalSales - a.totalSales), [data, isAr])
  const paymentTotal = data?.paymentBreakdown.reduce((sum, x) => sum + x.totalAmount, 0) ?? 0
  const payment = useMemo(() => (data?.paymentBreakdown ?? []).map((x) => ({
    ...x,
    name: t(`reports.payment.${x.paymentMethod.toLowerCase()}`),
    percentage: paymentTotal ? (x.totalAmount / paymentTotal) * 100 : 0,
    color: x.paymentMethod.toLowerCase() === 'card' ? CHART_2 : CHART_1,
  })), [data, paymentTotal, t])

  const channelTotal = channelSales.reduce((sum, x) => sum + x.totalSales, 0)
  const channelChart = useMemo(() => {
    const sorted = [...channelSales].sort((a, b) => b.totalSales - a.totalSales)
    const rows = sorted.slice(0, 5).map((x, i) => ({ name: isAr ? x.nameAr : x.nameEn, totalSales: x.totalSales, color: CHANNEL_COLORS[i] }))
    const restTotal = sorted.slice(5).reduce((sum, x) => sum + x.totalSales, 0)
    if (restTotal > 0) rows.push({ name: t('reports.other'), totalSales: restTotal, color: MUTED })
    return rows.map((row) => ({ ...row, percentage: channelTotal ? (row.totalSales / channelTotal) * 100 : 0 }))
  }, [channelSales, channelTotal, isAr, t])

  const shiftVariances = useMemo(() => (data?.shiftVariances ?? []).map((x) => ({
    ...x,
    label: new Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'short' }).format(new Date(x.openedAt)),
  })), [data, i18n.language])

  return <section>
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div className="grid gap-1"><h1>{t('reports.dashboardTitle')}</h1><p className="text-sm text-muted">{t('reports.dashboardSubtitle')}</p></div>
      <div className="flex flex-wrap gap-2 rounded-xl border border-border bg-surface p-3">
        {hasPermission('reports.global.view') && !user?.branchId && <label className="report-filter">{t('reports.branch')}<select value={branchId} onChange={(e) => setBranchId(e.target.value)}><option value="">{t('reports.global')}</option>{branches.map((b) => <option key={b.id} value={b.id}>{name(b.nameAr, b.nameEn)}</option>)}</select></label>}
        <label className="report-filter">{t('reports.from')}<input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} /></label>
        <label className="report-filter">{t('reports.to')}<input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} /></label>
      </div>
    </div>
    {error && <p className="error-text">{error}</p>}
    {loading && <ReportsSkeleton />}
    {data && !loading && <>
      <div className="report-stat-grid grid gap-4 md:grid-cols-3">
        <StatCard tone="primary" label={t('reports.totalSales')} value={<Money value={data.totalSales} />} icon={<SalesIcon className="h-5 w-5" />} />
        <StatCard tone="accent" label={t('reports.invoices')} value={data.invoiceCount.toLocaleString()} icon={<InvoiceIcon className="h-5 w-5" />} />
        <StatCard tone="primary" label={t('reports.itemsSold')} value={data.itemsSold.toLocaleString()} icon={<ItemsIcon className="h-5 w-5" />} />
        <StatCard tone="accent" label={t('reports.averageTicket')} value={<Money value={data.averageTicket} />} icon={<TicketIcon className="h-5 w-5" />} />
        <StatCard tone="danger" label={t('reports.totalDiscounts')} value={<Money value={data.totalDiscounts} />} icon={<TicketIcon className="h-5 w-5" />} />
        <StatCard tone={lowStockCount ? 'danger' : 'primary'} label={t('reports.lowStockAlerts')} value={lowStockCount.toLocaleString()} icon={<ItemsIcon className="h-5 w-5" />} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
      <ChartCard title={t('reports.channelDistribution')}>
          <div className="relative h-full min-h-0">
            <ChartCanvas>
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={channelChart} dataKey="totalSales" nameKey="name" innerRadius="32%" outerRadius="92%" paddingAngle={channelChart.length > 1 ? 2 : 0} strokeWidth={2} stroke="rgb(var(--color-surface))" label={renderPieSliceLabel} labelLine={false}>
                    {channelChart.map((c, i) => <Cell key={i} fill={c.color} />)}
                  </Pie>
                  <Tooltip {...tooltipBase} formatter={(value) => Number(value).toFixed(3)} />
                </PieChart>
              </ResponsiveContainer>
            </ChartCanvas>
            <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-1">
              <span className="text-xs text-muted">{t('reports.totalSales')}</span>
              <strong className="text-xl text-text"><Money value={channelTotal} /></strong>
            </div>
          </div>
      </ChartCard>
      <ChartCard title={t('reports.shiftVariances')}>
        <ChartCanvas>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={shiftVariances} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
              <CartesianGrid stroke={BORDER} vertical={false} />
              <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }} />
              <YAxis tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }} />
              <Tooltip {...tooltipBase} formatter={(value) => [Number(value).toFixed(3), t('reports.variance')]} cursor={{ fill: 'rgb(var(--color-surface2))' }} />
              <ReferenceLine y={0} stroke={BORDER} />
              <Bar dataKey="varianceAmount" radius={[4, 4, 4, 4]} maxBarSize={22}>
                {shiftVariances.map((x, i) => <Cell key={i} fill={x.varianceAmount < 0 ? DANGER : PRIMARY} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </ChartCanvas>
      </ChartCard>
      <ChartCard title={t('reports.salesTrend')}>
        <ChartCanvas>
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={trend} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="trendWash" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={CHART_1} stopOpacity={0.18} />
                  <stop offset="100%" stopColor={CHART_1} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke={BORDER} vertical={false} />
              <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 12 }} />
              <YAxis tickLine={false} axisLine={false} width={48} tick={{ fill: MUTED, fontSize: 12 }} />
              <Tooltip {...tooltipBase} formatter={(value) => [Number(value).toFixed(3), t('reports.totalSales')]} cursor={{ stroke: BORDER, strokeWidth: 1 }} />
              <Area type="monotone" dataKey="totalSales" stroke="none" fill="url(#trendWash)" isAnimationActive={false} tooltipType="none" legendType="none" />
              <Line type="monotone" dataKey="totalSales" name={t('reports.totalSales')} stroke={CHART_1} strokeWidth={2} dot={false} activeDot={{ r: 5, fill: CHART_1, strokeWidth: 2, stroke: 'rgb(var(--color-surface))' }} />
            </ComposedChart>
          </ResponsiveContainer>
        </ChartCanvas>
      </ChartCard>

      <ChartCard title={t('reports.invoiceTrend')}>
        <ChartCanvas><ResponsiveContainer width="100%" height="100%"><BarChart data={trend} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}><CartesianGrid stroke={BORDER} vertical={false}/><XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }}/><YAxis tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }}/><Tooltip {...tooltipBase} formatter={(value) => [Number(value).toLocaleString(), t('reports.invoices')]}/><Bar dataKey="invoiceCount" fill={CHART_2} radius={[5, 5, 0, 0]} maxBarSize={18}/></BarChart></ResponsiveContainer></ChartCanvas>
      </ChartCard>

      <ChartCard title={t('reports.itemsTrend')}>
        <ChartCanvas><ResponsiveContainer width="100%" height="100%"><ComposedChart data={trend} margin={{ top: 8, right: 8, left: -8, bottom: 0 }}><defs><linearGradient id="itemsWash" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={CHART_2} stopOpacity={0.25}/><stop offset="100%" stopColor={CHART_2} stopOpacity={0}/></linearGradient></defs><CartesianGrid stroke={BORDER} vertical={false}/><XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }}/><YAxis tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }}/><Tooltip {...tooltipBase} formatter={(value) => [Number(value).toLocaleString(), t('reports.itemsSold')]}/><Area type="monotone" dataKey="itemsSold" stroke={CHART_2} strokeWidth={2} fill="url(#itemsWash)"/></ComposedChart></ResponsiveContainer></ChartCanvas>
      </ChartCard>

      <ChartCard title={t('reports.averageTicketTrend')}>
        <ChartCanvas><ResponsiveContainer width="100%" height="100%"><ComposedChart data={trend} margin={{ top: 8, right: 8, left: -8, bottom: 0 }}><CartesianGrid stroke={BORDER} vertical={false}/><XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }}/><YAxis tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 11 }}/><Tooltip {...tooltipBase} formatter={(value) => [Number(value).toFixed(3), t('reports.averageTicket')]}/><Line type="monotone" dataKey="averageTicket" stroke={CHART_1} strokeWidth={2.5} dot={false} activeDot={{ r: 5, fill: CHART_1 }}/></ComposedChart></ResponsiveContainer></ChartCanvas>
      </ChartCard>

        <ChartCard title={t('reports.paymentDistribution')}>
            <div className="relative h-full min-h-0">
              <ChartCanvas>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie data={payment} dataKey="totalAmount" nameKey="name" innerRadius="32%" outerRadius="92%" paddingAngle={payment.length > 1 ? 2 : 0} strokeWidth={2} stroke="rgb(var(--color-surface))" label={renderPieSliceLabel} labelLine={false}>
                      {payment.map((p, i) => <Cell key={i} fill={p.color} />)}
                    </Pie>
                    <Tooltip {...tooltipBase} formatter={(value) => Number(value).toFixed(3)} />
                  </PieChart>
                </ResponsiveContainer>
              </ChartCanvas>
              <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-1">
                <span className="text-xs text-muted">{t('reports.totalSales')}</span>
                <strong className="text-xl text-text"><Money value={paymentTotal} /></strong>
              </div>
            </div>
        </ChartCard>

        <ChartCard title={t('reports.branchComparison')}>
          <ChartCanvas>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={branchData} layout="vertical" margin={{ top: 4, right: 52, bottom: 4, left: 4 }}>
                <CartesianGrid stroke={BORDER} horizontal={false} />
                <XAxis type="number" hide />
                <YAxis type="category" dataKey="name" width={92} axisLine={false} tickLine={false} tick={{ fill: 'rgb(var(--color-text))', fontSize: 13, fontWeight: 600 }} />
                <Tooltip {...tooltipBase} formatter={(value) => [Number(value).toFixed(3), t('reports.totalSales')]} cursor={{ fill: 'rgb(var(--color-surface2))' }} />
                <Bar dataKey="totalSales" fill={CHART_1} radius={[0, 4, 4, 0]} maxBarSize={20}>
                  <LabelList dataKey="totalSales" position="right" formatter={(v) => Number(v).toFixed(3)} fill={MUTED} fontSize={12} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </ChartCanvas>
        </ChartCard>

        <ChartCard title={t('reports.topProducts')}>
          <ChartCanvas>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={topProducts} layout="vertical" margin={{ top: 4, right: 40, bottom: 4, left: 4 }}>
                <CartesianGrid stroke={BORDER} horizontal={false} />
                <XAxis type="number" hide />
                <YAxis type="category" dataKey="name" width={100} axisLine={false} tickLine={false} tick={{ fill: 'rgb(var(--color-text))', fontSize: 13, fontWeight: 600 }} />
                <Tooltip {...tooltipBase} formatter={(value) => [Number(value).toLocaleString(), t('reports.quantitySold')]} cursor={{ fill: 'rgb(var(--color-surface2))' }} />
                <Bar dataKey="quantitySold" fill={CHART_1} radius={[0, 4, 4, 0]} maxBarSize={20}>
                  <LabelList dataKey="quantitySold" position="right" formatter={(v) => Number(v).toLocaleString()} fill={MUTED} fontSize={12} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </ChartCanvas>
        </ChartCard>

        <ChartCard title={t('reports.topProductsRevenue')}>
          <ChartCanvas><ResponsiveContainer width="100%" height="100%"><BarChart data={topRevenueProducts} layout="vertical" margin={{ top: 4, right: 48, bottom: 4, left: 4 }}><CartesianGrid stroke={BORDER} horizontal={false}/><XAxis type="number" hide/><YAxis type="category" dataKey="name" width={100} axisLine={false} tickLine={false} tick={{ fill: 'rgb(var(--color-text))', fontSize: 13, fontWeight: 600 }}/><Tooltip {...tooltipBase} formatter={(value) => [Number(value).toFixed(3), t('reports.totalSales')]}/><Bar dataKey="totalSales" fill={CHART_2} radius={[0, 4, 4, 0]} maxBarSize={20}><LabelList dataKey="totalSales" position="right" formatter={(v) => Number(v).toFixed(1)} fill={MUTED} fontSize={12}/></Bar></BarChart></ResponsiveContainer></ChartCanvas>
        </ChartCard>
      </div>

      <button type="button" onClick={()=>setShowTable(value=>!value)}>{showTable?t('reports.hideTable'):t('reports.showTable')}</button>
      {showTable && <div className="rounded-xl border border-border bg-surface p-4">
        <div className="table-toolbar"><h2>{t('reports.productDetails')}</h2><SearchBox value={productSearch} onChange={(e) => setProductSearch(e.target.value)} placeholder={t('reports.searchProducts')} /></div>
        <div className="table-shell"><table><thead><tr>
          <SortableHeader label={t('reports.product')} sortKey="product" sort={productSort} onSort={toggleProductSort} />
          <SortableHeader label={t('reports.quantitySold')} sortKey="quantitySold" sort={productSort} onSort={toggleProductSort} />
          <SortableHeader label={t('reports.totalSales')} sortKey="totalSales" sort={productSort} onSort={toggleProductSort} />
          <SortableHeader label={t('reports.invoices')} sortKey="invoiceCount" sort={productSort} onSort={toggleProductSort} />
          <SortableHeader label={t('reports.share')} sortKey="share" sort={productSort} onSort={toggleProductSort} />
        </tr></thead><tbody>{sortedProducts.map((x) => <tr key={x.productId}><td className="font-bold">{name(x.nameAr, x.nameEn)}</td><td>{x.quantitySold.toLocaleString()}</td><td><Money value={x.totalSales} /></td><td>{x.invoiceCount}</td><td>{data.totalSales ? `${((x.totalSales / data.totalSales) * 100).toFixed(1)}%` : '0%'}</td></tr>)}</tbody></table></div>
      </div>}
    </>}
  </section>
}

function SortableHeader({ label, sortKey, sort, onSort }: { label: string; sortKey: ProductSortKey; sort: { key: ProductSortKey; direction: SortDirection }; onSort: (key: ProductSortKey) => void }) {
  const active = sort.key === sortKey
  return <th aria-sort={active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'}>
    <button type="button" className="report-sort-button" onClick={() => onSort(sortKey)}>
      <span>{label}</span><span aria-hidden="true" className={active ? 'is-active' : ''}>{active && sort.direction === 'asc' ? '↑' : '↓'}</span>
    </button>
  </th>
}

function StatCard({ label, value, icon, tone }: { label: string; value: ReactNode; icon: ReactNode; tone: 'primary' | 'accent' | 'danger' }) {
  const toneClass = tone === 'accent' ? 'text-accent bg-accent/10' : tone === 'danger' ? 'text-danger bg-danger/10' : 'text-primary bg-primary/10'
  const valueClass = tone === 'accent' ? 'text-accent' : tone === 'danger' ? 'text-danger' : 'text-primary'
  return <div className="ui-card ui-card-interactive group grid gap-3">
    <div className="flex items-start justify-between">
      <span className="text-sm font-medium text-muted">{label}</span>
      <span className={`flex h-10 w-10 items-center justify-center rounded-lg transition-colors ${toneClass}`}>{icon}</span>
    </div>
    <strong className={`block truncate font-cairo text-3xl ${valueClass}`}>{value}</strong>
  </div>
}
function ChartCard({ title, children, className = 'h-96' }: { title: string; children: ReactNode; className?: string }) {
  return <div className={`report-chart-card ui-card grid gap-4 p-4 ${className}`}>
    <h2 className="truncate text-base font-bold">{title}</h2>
    <div className="min-h-0">{children}</div>
  </div>
}

function renderPieSliceLabel({ cx, cy, midAngle, innerRadius, outerRadius, name, percent }: PieLabelRenderProps) {
  const share = Number(percent ?? 0)
  if (share < 0.055) return null
  const radius = Number(innerRadius) + (Number(outerRadius) - Number(innerRadius)) * 0.58
  const angle = -Number(midAngle) * Math.PI / 180
  const x = Number(cx) + radius * Math.cos(angle)
  const y = Number(cy) + radius * Math.sin(angle)
  const label = String(name ?? '')
  const shortLabel = label.length > 13 ? `${label.slice(0, 12)}…` : label
  return <text x={x} y={y} fill="white" textAnchor="middle" dominantBaseline="central" className="report-pie-label">
    <tspan x={x} dy="-0.45em">{shortLabel}</tspan>
    <tspan x={x} dy="1.25em">{(share * 100).toFixed(0)}%</tspan>
  </text>
}
function ReportsSkeleton() {
  return <div className="reports-skeleton grid gap-6">
    <div className="grid gap-4 md:grid-cols-3">
      {Array.from({ length: 6 }).map((_, i) => <div key={i} className="reports-skeleton-block h-[6.5rem] rounded-xl" />)}
    </div>
    <div className="grid gap-4 lg:grid-cols-2">
      {Array.from({ length: 10 }).map((_, i) => <div key={i} className="reports-skeleton-block h-96 rounded-xl" />)}
    </div>
  </div>
}

// SVG text-anchor is direction-aware: under an inherited dir="rtl", recharts' "end"-anchored
// axis-tick labels flip to hang the wrong way and land on top of the plot. Charts always lay
// out left-to-right regardless of UI language, so pin each chart canvas to ltr.
function ChartCanvas({ children }: { children: ReactNode }) {
  return <div dir="ltr" className="h-full w-full">{children}</div>
}

function SalesIcon(props: SVGProps<SVGSVGElement>) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><path d="M3 17l6-6 4 4 7-8" /><path d="M14 7h6v6" /></svg>
}
function InvoiceIcon(props: SVGProps<SVGSVGElement>) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><path d="M6 3h12v17l-3-2-3 2-3-2-3 2V3Z" /><path d="M9 8h6M9 12h4" /></svg>
}
function ItemsIcon(props: SVGProps<SVGSVGElement>) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><path d="M12 3 3 8l9 5 9-5-9-5Z" /><path d="M3 13l9 5 9-5" /></svg>
}
function TicketIcon(props: SVGProps<SVGSVGElement>) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><path d="M12.6 3.4 20 10.8a2 2 0 0 1 0 2.8l-6.4 6.4a2 2 0 0 1-2.8 0L3.4 12.6a2 2 0 0 1-.6-1.4V5a1.6 1.6 0 0 1 1.6-1.6h6.2c.5 0 1 .2 1.4.6Z" /><circle cx="8" cy="8" r="1.2" /></svg>
}
