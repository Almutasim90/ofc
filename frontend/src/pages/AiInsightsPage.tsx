import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BranchDto } from '../api/types'

interface AiInsightDto { id: string; requestType: string; result: string; createdAt: string }

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

const presets: { requestType: string; labelKey: string }[] = [
  { requestType: 'Summary', labelKey: 'ai.summary' },
  { requestType: 'Anomaly detection', labelKey: 'ai.anomaly' },
  { requestType: 'Demand forecast', labelKey: 'ai.forecast' },
]

export default function AiInsightsPage() {
  const { t, i18n } = useTranslation()
  const { user, hasPermission } = useAuth()
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [branchId, setBranchId] = useState(user?.branchId ?? '')
  const [from, setFrom] = useState(isoMonthStart)
  const [to, setTo] = useState(isoToday)
  const [question, setQuestion] = useState('')
  const [busyType, setBusyType] = useState<string | null>(null)
  const [result, setResult] = useState<AiInsightDto | null>(null)
  const [recent, setRecent] = useState<AiInsightDto[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { api.get<BranchDto[]>('/api/branches').then((rows) => setBranches(rows.filter((b) => b.isActive))).catch(() => {}) }, [])
  const loadRecent = () => { api.get<AiInsightDto[]>('/api/ai/insights?take=10').then(setRecent).catch(() => {}) }
  useEffect(loadRecent, [])

  const generate = async (requestType: string, customQuestion?: string) => {
    setBusyType(requestType); setError(null)
    try {
      const insight = await api.post<AiInsightDto>('/api/ai/insights', { requestType, from, to, branchId: branchId || null, question: customQuestion })
      setResult(insight)
      loadRecent()
      if (customQuestion) setQuestion('')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('aiInsights.error'))
    } finally {
      setBusyType(null)
    }
  }
  const submitCustom = (e: FormEvent) => { e.preventDefault(); if (question.trim()) generate('Custom', question.trim()) }
  const branchName = (branch: BranchDto) => (i18n.language === 'ar' ? branch.nameAr : branch.nameEn)

  return <section className="ui-stack">
    <div className="grid gap-1"><h1>{t('aiInsights.title')}</h1><p className="text-sm text-muted">{t('aiInsights.description')}</p></div>

    <div className="flex flex-wrap gap-2 rounded-xl border border-border bg-surface p-3">
      {hasPermission('reports.global.view') && !user?.branchId && <label className="report-filter">{t('reports.branch')}<select value={branchId} onChange={(e) => setBranchId(e.target.value)}><option value="">{t('reports.global')}</option>{branches.map((b) => <option key={b.id} value={b.id}>{branchName(b)}</option>)}</select></label>}
      <label className="report-filter">{t('reports.from')}<input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} /></label>
      <label className="report-filter">{t('reports.to')}<input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} /></label>
    </div>

    <div className="ui-card ui-stack">
      <h2>{t('aiInsights.presetsTitle')}</h2>
      <div className="flex flex-wrap gap-3">
        {presets.map((preset) => <button key={preset.requestType} type="button" onClick={() => generate(preset.requestType)} disabled={busyType !== null}>
          {busyType === preset.requestType ? t('common.loading') : t(preset.labelKey)}
        </button>)}
      </div>
    </div>

    <form className="ui-card ui-stack" onSubmit={submitCustom}>
      <h2>{t('aiInsights.customTitle')}</h2>
      <label className="flex flex-col gap-1 text-muted">
        {t('aiInsights.questionLabel')}
        <textarea rows={3} required placeholder={t('aiInsights.questionPlaceholder')} value={question} onChange={(e) => setQuestion(e.target.value)} />
      </label>
      <button disabled={busyType !== null} className="justify-self-start">{busyType === 'Custom' ? t('common.loading') : t('aiInsights.ask')}</button>
    </form>

    {error && <p className="error-text" role="alert">{error}</p>}

    {result && <div className="ui-card ui-stack">
      <h2>{result.requestType}</h2>
      <p className="ai-insight-result">{result.result}</p>
      <span className="text-xs text-muted">{new Date(result.createdAt).toLocaleString(i18n.language)}</span>
    </div>}

    {recent.length > 0 && <div className="ui-card ui-stack">
      <h2>{t('aiInsights.recentTitle')}</h2>
      <ul className="ai-insight-history">
        {recent.map((item) => <li key={item.id}>
          <button type="button" className="ai-insight-history-item" onClick={() => setResult(item)}>
            <span className="truncate">{item.requestType}</span>
            <span className="text-xs text-muted">{new Date(item.createdAt).toLocaleString(i18n.language)}</span>
          </button>
        </li>)}
      </ul>
    </div>}
  </section>
}
