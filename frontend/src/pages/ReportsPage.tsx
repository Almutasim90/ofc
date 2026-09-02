import { useEffect, useMemo, useState, type ReactNode, type SVGProps } from 'react'
import { useTranslation } from 'react-i18next'
import { Area, Bar, BarChart, CartesianGrid, Cell, ComposedChart, LabelList, Legend, Line, Pie, PieChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis, type LegendPayload, type PieLabelRenderProps } from 'recharts'
import { api } from '../api/client'
import type { BranchDto, ChannelSalesDto, ManagerDashboardDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import DataTable from '../components/DataTable'
import Money from '../components/Money'

const formatLocalDate = (date: Date) => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const isoToday = formatLocalDate(new Date())
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
type PaymentFilter = 'all' | 'cash' | 'card'

function downloadCsv(filename: string, rows: (string | number)[][]) {
  const csv = rows.map((row) => row.map((cell) => {
    const value = String(cell)
    return /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value
  }).join(',')).join('\r\n')
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

const tooltipBase = {
  contentStyle: { borderRadius: 12, border: `1px solid ${BORDER}`, background: 'rgb(var(--color-surface))', color: 'rgb(var(--color-text))', fontSize: 14, padding: '0.5rem 0.75rem', boxShadow: 'var(--shadow-card-hover)' },
  labelStyle: { color: MUTED, marginBottom: 4, fontWeight: 600 },
  itemStyle: { color: 'rgb(var(--color-text))' },
}

export default function ReportsPage() {
  const { t, i18n } = useTranslation()
  const { user, hasPermission } = useAuth()
  const [from, setFrom] = useState(isoToday)
  const [to, setTo] = useState(isoToday)
  const [branchId, setBranchId] = useState(user?.branchId ?? '')
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [data, setData] = useState<ManagerDashboardDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [channelSales, setChannelSales] = useState<ChannelSalesDto[]>([])
  const [lowStockCount, setLowStockCount] = useState(0)
  const [showTable, setShowTable] = useState(false)
  const [paymentFilter, setPaymentFilter] = useState<PaymentFilter>('all')

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
  const cashSales = data?.paymentBreakdown.find((x) => x.paymentMethod.toLowerCase() === 'cash')?.totalAmount ?? 0
  const cardSales = data?.paymentBreakdown.find((x) => x.paymentMethod.toLowerCase() === 'card')?.totalAmount ?? 0
  const products = useMemo(() => {
    const rows = data?.products ?? []
    if (paymentFilter === 'cash') return rows.filter((x) => x.cashTotalSales > 0).map((x) => ({ ...x, quantitySold: x.cashQuantitySold, totalSales: x.cashTotalSales, invoiceCount: x.cashInvoiceCount }))
    if (paymentFilter === 'card') return rows.filter((x) => x.cardTotalSales > 0).map((x) => ({ ...x, quantitySold: x.cardQuantitySold, totalSales: x.cardTotalSales, invoiceCount: x.cardInvoiceCount }))
    return rows
  }, [data, paymentFilter])
  const shareBase = paymentFilter === 'cash' ? cashSales : paymentFilter === 'card' ? cardSales : (data?.totalSales ?? 0)
  const sortedProducts = useMemo(() => [...products].sort((a, b) => b.totalSales - a.totalSales), [products])
  const exportCsv = () => {
    if (!data) return
    const rows: (string | number)[][] = [
      [t('reports.dashboardTitle')],
      [t('reports.from'), from, t('reports.to'), to],
      [],
      [t('reports.totalSales'), data.totalSales.toFixed(3)],
      [t('reports.invoices'), data.invoiceCount],
      [t('reports.averageTicket'), data.averageTicket.toFixed(3)],
      [t('reports.cashSales'), cashSales.toFixed(3)],
      [t('reports.cardSales'), cardSales.toFixed(3)],
      [t('reports.totalDiscounts'), data.totalDiscounts.toFixed(3)],
      [],
      [t('reports.product'), t('reports.quantitySold'), t('reports.totalSales'), t('reports.invoices'), t('reports.share')],
      ...sortedProducts.map((x) => [
        name(x.nameAr, x.nameEn), x.quantitySold, x.totalSales.toFixed(3), x.invoiceCount,
        shareBase ? `${((x.totalSales / shareBase) * 100).toFixed(1)}%` : '0%',
      ]),
    ]
    downloadCsv(`sales-report-${from}-to-${to}.csv`, rows)
  }
  const isAr = i18n.language === 'ar'
  const topProducts = useMemo(() => (data?.products ?? []).map((x) => ({ ...x, name: isAr ? x.nameAr : x.nameEn })).sort((a, b) => b.quantitySold - a.quantitySold).slice(0, 6), [data, isAr])
  const topRevenueProducts = useMemo(() => (data?.products ?? []).map((x) => ({ ...x, name: isAr ? x.nameAr : x.nameEn })).sort((a, b) => b.totalSales - a.totalSales).slice(0, 6), [data, isAr])
  const branchData = useMemo(() => (data?.branches ?? []).map((x) => ({ ...x, name: isAr ? x.branchNameAr : x.branchNameEn })).sort((a, b) => b.totalSales - a.totalSales), [data, isAr])
  const paymentTotal = data?.paymentBreakdown.reduce((sum, x) => sum + x.totalAmount, 0) ?? 0
  const payment = useMemo(() => (data?.paymentBreakdown ?? []).map((x) => ({
    ...x,
    name: t(`reports.payment.${x.paymentMethod.toLowerCase()}`),
    percentage: paymentTotal ? (x.totalAmount / paymentTotal) * 100 : 0,
    color: x.paymentMethod.toLowerCase() === 'card' ? CHART_1 : PRIMARY,
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
      <div className="flex flex-wrap items-end gap-2">
        <div className="flex flex-wrap gap-2 rounded-xl border border-border bg-surface p-3">
          {hasPermission('reports.global.view') && !user?.branchId && <label className="report-filter">{t('reports.branch')}<select value={branchId} onChange={(e) => setBranchId(e.target.value)}><option value="">{t('reports.global')}</option>{branches.map((b) => <option key={b.id} value={b.id}>{name(b.nameAr, b.nameEn)}</option>)}</select></label>}
          <label className="report-filter">{t('reports.from')}<input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} /></label>
          <label className="report-filter">{t('reports.to')}<input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} /></label>
        </div>
        {data && <button type="button" onClick={exportCsv}>{t('reports.exportCsv')}</button>}
      </div>
    </div>
    {error && <p className="error-text">{error}</p>}
    {loading && <ReportsSkeleton />}
    {data && !loading && <>
      <div className="report-stat-grid grid gap-4 md:grid-cols-3 report-content-enter">
        <StatCard tone="primary" label={t('reports.totalSales')} value={<Money value={data.totalSales} />} icon={<SalesIcon className="h-5 w-5" />} />
        <StatCard tone="accent" label={t('reports.invoices')} value={data.invoiceCount.toLocaleString()} icon={<InvoiceIcon className="h-5 w-5" />} />
        <StatCard tone="primary" label={t('reports.itemsSold')} value={data.itemsSold.toLocaleString()} icon={<ItemsIcon className="h-5 w-5" />} />
        <StatCard tone="accent" label={t('reports.averageTicket')} value={<Money value={data.averageTicket} />} icon={<TicketIcon className="h-5 w-5" />} />
        <StatCard tone="cash" label={t('reports.cashSales')} value={<Money value={cashSales} />} icon={<CashIcon className="h-5 w-5" />} />
        <StatCard tone="card" label={t('reports.cardSales')} value={<Money value={cardSales} />} icon={<CardIcon className="h-5 w-5" />} />
        <StatCard tone="danger" label={t('reports.totalDiscounts')} value={<Money value={data.totalDiscounts} />} icon={<TicketIcon className="h-5 w-5" />} />
        <StatCard tone={lowStockCount ? 'danger' : 'primary'} label={t('reports.lowStockAlerts')} value={lowStockCount.toLocaleString()} icon={<ItemsIcon className="h-5 w-5" />} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2 report-content-enter" style={{ animationDelay: '80ms' }}>
       <ChartCard title={t('reports.orderTypeDistribution')}><DataTable rows={data.orderTypes} pageSize={0} queryPrefix="order-types" getRowKey={x=>x.code} columns={[
         {id:'type',header:t('common.type'),cell:x=>name(x.nameAr,x.nameEn),sortValue:x=>name(x.nameAr,x.nameEn)},
         {id:'invoices',header:t('reports.invoices'),cell:x=>x.invoiceCount,sortValue:x=>x.invoiceCount},
         {id:'sales',header:t('reports.totalSales'),cell:x=><Money value={x.totalSales}/>,sortValue:x=>x.totalSales},
       ]}/></ChartCard>
       <ChartCard title={t('reports.cashShiftVarianceReport')}><DataTable rows={data.cashShiftVariances} pageSize={0} queryPrefix="cash-variance" defaultSort={{id:'date',direction:'desc'}} getRowKey={x=>x.cashShiftId} columns={[
         {id:'date',header:t('reports.date'),cell:x=>new Date(x.openedAt).toLocaleString(i18n.language),sortValue:x=>new Date(x.openedAt)},
         {id:'expected',header:t('cashShifts.expected'),cell:x=><Money value={x.expectedCash}/>,sortValue:x=>x.expectedCash},
         {id:'counted',header:t('cashShifts.counted'),cell:x=><Money value={x.countedCash}/>,sortValue:x=>x.countedCash},
         {id:'variance',header:t('cashShifts.variance'),cell:x=><Money value={x.varianceCash}/>,sortValue:x=>x.varianceCash},
       ]}/></ChartCard>
       <ChartCard title={t('reports.orderEditReport')}><DataTable rows={data.orderEdits} pageSize={0} queryPrefix="order-edits" defaultSort={{id:'date',direction:'desc'}} getRowKey={x=>x.id} columns={[
         {id:'invoice',header:t('reports.invoice'),cell:x=>`#${x.orderNumber}`,sortValue:x=>x.orderNumber},
         {id:'date',header:t('reports.date'),cell:x=>new Date(x.createdAt).toLocaleString(i18n.language),sortValue:x=>new Date(x.createdAt)},
         {id:'type',header:t('common.type'),cell:x=>x.editType,sortValue:x=>x.editType},
         {id:'variance',header:t('cashShifts.variance'),cell:x=><Money value={x.amountDelta}/>,sortValue:x=>x.amountDelta},
       ]}/></ChartCard>
      <ChartCard title={t('reports.channelDistribution')}>
          <div className="relative h-full min-h-0">
            <ChartCanvas>
              <ResponsiveContainer width="100%" height="100%">
                <PieChart margin={{ top: 12, right: 64, bottom: 4, left: 64 }}>
                  <Pie data={channelChart} dataKey="totalSales" nameKey="name" innerRadius="34%" outerRadius="56%" paddingAngle={channelChart.length > 1 ? 2 : 0} strokeWidth={2} stroke="rgb(var(--color-surface))" label={renderPieOuterLabel} labelLine={false}>
                    {channelChart.map((c, i) => <Cell key={i} fill={c.color} />)}
                  </Pie>
                  <Tooltip {...tooltipBase} formatter={(value, itemName, item) => [`${Number(value).toFixed(3)} (${Number(item.payload?.percentage ?? 0).toFixed(1)}%)`, itemName]} />
                  <Legend verticalAlign="bottom" height={30} iconType="circle" iconSize={8} wrapperStyle={{ fontSize: 12 }} formatter={renderPieLegendLabel} />
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
                  <stop offset="0%" stopColor={PRIMARY} stopOpacity={0.14} />
                  <stop offset="100%" stopColor={PRIMARY} stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke={BORDER} vertical={false} />
              <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: MUTED, fontSize: 12 }} />
              <YAxis tickLine={false} axisLine={false} width={48} tick={{ fill: MUTED, fontSize: 12 }} />
              <Tooltip {...tooltipBase} formatter={(value, itemName) => [Number(value).toFixed(3), itemName]} cursor={{ stroke: BORDER, strokeWidth: 1 }} />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              <Area type="monotone" dataKey="totalSales" stroke="none" fill="url(#trendWash)" isAnimationActive={false} tooltipType="none" legendType="none" />
              <Line type="monotone" dataKey="totalSales" name={t('reports.totalSales')} stroke={MUTED} strokeWidth={1.5} strokeDasharray="4 3" dot={false} activeDot={{ r: 4, fill: MUTED }} />
              <Line type="monotone" dataKey="cashSales" name={t('reports.cashSales')} stroke={PRIMARY} strokeWidth={2} dot={false} activeDot={{ r: 5, fill: PRIMARY, strokeWidth: 2, stroke: 'rgb(var(--color-surface))' }} />
              <Line type="monotone" dataKey="cardSales" name={t('reports.cardSales')} stroke={CHART_1} strokeWidth={2} dot={false} activeDot={{ r: 5, fill: CHART_1, strokeWidth: 2, stroke: 'rgb(var(--color-surface))' }} />
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
                  <PieChart margin={{ top: 12, right: 64, bottom: 4, left: 64 }}>
                    <Pie data={payment} dataKey="totalAmount" nameKey="name" innerRadius="34%" outerRadius="56%" paddingAngle={payment.length > 1 ? 2 : 0} strokeWidth={2} stroke="rgb(var(--color-surface))" label={renderPieOuterLabel} labelLine={false}>
                      {payment.map((p, i) => <Cell key={i} fill={p.color} />)}
                    </Pie>
                    <Tooltip {...tooltipBase} formatter={(value, itemName, item) => [`${Number(value).toFixed(3)} (${Number(item.payload?.percentage ?? 0).toFixed(1)}%)`, itemName]} />
                    <Legend verticalAlign="bottom" height={30} iconType="circle" iconSize={8} wrapperStyle={{ fontSize: 12 }} formatter={renderPieLegendLabel} />
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
      {showTable && <div className="rounded-xl border border-border bg-surface p-4 report-content-enter">
        <h2>{t('reports.productDetails')}</h2>
        <DataTable rows={products} queryPrefix="products" defaultSort={{id:'totalSales',direction:'desc'}} getRowKey={x=>x.productId} getSearchText={x=>`${x.nameAr} ${x.nameEn}`} searchPlaceholder={t('reports.searchProducts')}
          toolbar={<div className="flex flex-wrap gap-2">
            <label className="report-filter">{t('reports.paymentMethod')}<select value={paymentFilter} onChange={(e) => setPaymentFilter(e.target.value as PaymentFilter)}>
              <option value="all">{t('reports.allPaymentMethods')}</option>
              <option value="cash">{t('reports.payment.cash')}</option>
              <option value="card">{t('reports.payment.card')}</option>
            </select></label>
          </div>}
          columns={[
            {id:'product',header:t('reports.product'),cell:x=><span className="font-bold">{name(x.nameAr,x.nameEn)}</span>,sortValue:x=>name(x.nameAr,x.nameEn)},
            {id:'quantitySold',header:t('reports.quantitySold'),cell:x=>x.quantitySold.toLocaleString(),sortValue:x=>x.quantitySold},
            {id:'totalSales',header:t('reports.totalSales'),cell:x=><Money value={x.totalSales}/>,sortValue:x=>x.totalSales},
            {id:'invoiceCount',header:t('reports.invoices'),cell:x=>x.invoiceCount,sortValue:x=>x.invoiceCount},
            {id:'share',header:t('reports.share'),cell:x=>shareBase?`${((x.totalSales/shareBase)*100).toFixed(1)}%`:'0%',sortValue:x=>x.totalSales},
          ]}/>
      </div>}
    </>}
  </section>
}

function StatCard({ label, value, icon, tone }: { label: string; value: ReactNode; icon: ReactNode; tone: 'primary' | 'accent' | 'danger' | 'cash' | 'card' }) {
  if (tone === 'cash' || tone === 'card') {
    return <div className={`ui-card ui-card-interactive group grid gap-3 report-stat-card-${tone}`}>
      <div className="flex items-start justify-between">
        <span className="text-sm font-medium">{label}</span>
        <span className="report-stat-icon flex h-10 w-10 items-center justify-center rounded-lg">{icon}</span>
      </div>
      <strong className="block truncate font-cairo text-3xl">{value}</strong>
    </div>
  }
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

// Labels sit outside the ring on a leader line color-matched to their slice, instead of
// crowding inside thin/small slices where text used to get clipped or unreadable.
function renderPieOuterLabel({ cx, cy, midAngle, outerRadius, name, percent, fill }: PieLabelRenderProps) {
  const share = Number(percent ?? 0)
  if (share < 0.02) return null
  const RADIAN = Math.PI / 180
  const angle = -Number(midAngle) * RADIAN
  const sin = Math.sin(angle)
  const cos = Math.cos(angle)
  const cxN = Number(cx)
  const cyN = Number(cy)
  const outerR = Number(outerRadius)
  const sx = cxN + (outerR + 4) * cos
  const sy = cyN + (outerR + 4) * sin
  const mx = cxN + (outerR + 18) * cos
  const my = cyN + (outerR + 18) * sin
  const side = cos >= 0 ? 1 : -1
  const ex = mx + side * 12
  const color = String(fill ?? MUTED)
  const label = String(name ?? '')
  const shortLabel = label.length > 14 ? `${label.slice(0, 13)}…` : label
  return <g>
    <path d={`M${sx},${sy}L${mx},${my}L${ex},${my}`} stroke={color} strokeWidth={1.5} fill="none" />
    <circle cx={sx} cy={sy} r={2.5} fill={color} stroke="none" />
    <text x={ex + side * 4} y={my - 3} textAnchor={side > 0 ? 'start' : 'end'} className="report-pie-label-outer">{shortLabel}</text>
    <text x={ex + side * 4} y={my + 13} textAnchor={side > 0 ? 'start' : 'end'} className="report-pie-label-outer-pct" fill={color}>{(share * 100).toFixed(0)}%</text>
  </g>
}
function renderPieLegendLabel(value: string, entry: LegendPayload) {
  const pct = Number((entry.payload as { percentage?: number } | undefined)?.percentage ?? NaN)
  return Number.isFinite(pct) ? `${value} — ${pct.toFixed(0)}%` : value
}
function ReportsSkeleton() {
  return <div className="reports-skeleton grid gap-6">
    <div className="grid gap-4 md:grid-cols-3">
      {Array.from({ length: 8 }).map((_, i) => <div key={i} className="reports-skeleton-block h-[6.5rem] rounded-xl" />)}
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
function CashIcon(props: SVGProps<SVGSVGElement>) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><rect x="2.5" y="6" width="19" height="12" rx="2" /><circle cx="12" cy="12" r="2.5" /><path d="M6 9v0M18 15v0" /></svg>
}
function CardIcon(props: SVGProps<SVGSVGElement>) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><rect x="2.5" y="5" width="19" height="14" rx="2.2" /><path d="M2.5 10h19" /><path d="M6 15h4" /></svg>
}
