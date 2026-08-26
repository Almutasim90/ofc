import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { useTheme } from '../theme/ThemeContext'
import { Link } from 'react-router-dom'

export default function SettingsPage() {
  const { t, i18n } = useTranslation(); const { user, updatePreferences, hasPermission } = useAuth(); const { theme } = useTheme()
  const [search, setSearch] = useState('')
  const [summaries, setSummaries] = useState<Record<string,string>>({})
  useEffect(()=>{const tasks:Promise<void>[]=[];if(hasPermission('closing.configure'))tasks.push(api.get<{defaultCloseTime:string;isActive:boolean}>('/api/closing-schedule/config').then(x=>setSummaries(s=>({...s,closing:`${x.defaultCloseTime.slice(0,5)} · ${x.isActive?t('common.active'):t('common.inactive')}`}))));if(hasPermission('users.manage'))tasks.push(api.get<unknown[]>('/api/users').then(x=>setSummaries(s=>({...s,users:t('settingsHub.count',{count:x.length})}))));if(hasPermission('branches.manage'))tasks.push(api.get<unknown[]>('/api/branches').then(x=>setSummaries(s=>({...s,branches:t('settingsHub.count',{count:x.length})}))));if(hasPermission('channels.manage'))tasks.push(api.get<unknown[]>('/api/channels').then(x=>setSummaries(s=>({...s,channels:t('settingsHub.count',{count:x.length})}))));if(hasPermission('ai.manage'))tasks.push(api.get<{provider:string;model:string;isActive:boolean}>('/api/ai/settings').then(x=>setSummaries(s=>({...s,ai:x.isActive?`${x.provider} · ${x.model}`:t('common.inactive')}))));void Promise.allSettled(tasks)},[hasPermission,t])
  const [language, setLanguage] = useState(user?.preferredLanguage ?? i18n.language)
  const [currentPassword, setCurrentPassword] = useState(''); const [newPassword, setNewPassword] = useState(''); const [confirmPassword, setConfirmPassword] = useState('')
  const [message, setMessage] = useState<string | null>(null); const [error, setError] = useState<string | null>(null)
  const savePreferences = async (event: FormEvent) => { event.preventDefault(); setError(null); setMessage(null); try { await api.put('/api/me/preferences', { preferredLanguage: language, preferredTheme: theme }); await i18n.changeLanguage(language); updatePreferences(language, theme); setMessage(t('settings.saved')) } catch { setError(t('settings.saveError')) } }
  const changePassword = async (event: FormEvent) => { event.preventDefault(); setError(null); setMessage(null); if (newPassword !== confirmPassword) { setError(t('settings.passwordMismatch')); return } try { await api.put('/api/me/password', { currentPassword, newPassword }); setCurrentPassword(''); setNewPassword(''); setConfirmPassword(''); setMessage(t('settings.passwordChanged')) } catch (err) { setError(err instanceof ApiError ? err.message : t('settings.saveError')) } }
  const allCards=[
    {to:'/branches',title:t('nav.branches'),summary:summaries.branches??t('settingsHub.branchesSummary'),permission:'branches.manage',group:'operations' as const},
    {to:'/users',title:t('nav.users'),summary:summaries.users??t('settingsHub.usersSummary'),permission:'users.manage',group:'operations' as const},
    {to:'/closing-schedule',title:t('nav.closingSchedule'),summary:summaries.closing??t('settingsHub.closingSummary'),permission:'closing.configure',group:'operations' as const},
    {to:'/channels',title:t('nav.channels'),summary:summaries.channels??t('settingsHub.channelsSummary'),permission:'channels.manage',group:'integrations' as const},
    {to:'/ai-settings',title:t('nav.aiSettings'),summary:summaries.ai??t('settingsHub.aiSummary'),permission:'ai.manage',group:'integrations' as const},
    {to:'/email-settings',title:t('nav.emailSettings'),summary:summaries.email??t('settingsHub.emailSummary'),permission:'email.manage',group:'integrations' as const},
  ].filter(c=>hasPermission(c.permission)&&`${c.title} ${c.summary}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()))
  const operationsCards=allCards.filter(c=>c.group==='operations')
  const integrationsCards=allCards.filter(c=>c.group==='integrations')
  const cardGrid=(items:typeof allCards)=><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{items.map(c=><Link key={c.to} to={c.to} className="ui-card ui-card-interactive ui-stack min-h-11"><h2 className="truncate">{c.title}</h2><p className="line-clamp-2">{c.summary}</p></Link>)}</div>
  return <section><h1>{t('settings.hubTitle')}</h1>{message && <p className="text-primary">{message}</p>}{error && <p className="error-text">{error}</p>}
    <div className="ui-stack">
      <input className="w-full max-w-md" placeholder={t('settingsHub.search')} value={search} onChange={e=>setSearch(e.target.value)}/>
      {operationsCards.length>0 && <div className="ui-stack"><h2>{t('settingsHub.groupOperations')}</h2>{cardGrid(operationsCards)}</div>}
      {integrationsCards.length>0 && <div className="ui-stack"><h2>{t('settingsHub.groupIntegrations')}</h2>{cardGrid(integrationsCards)}</div>}
    </div>
    <div className="ui-stack">
      <h2>{t('settingsHub.myAccount')}</h2>
      <div className="grid gap-6 lg:grid-cols-2">
        <form className="ui-card ui-stack" onSubmit={savePreferences}><h3>{t('settings.preferences')}</h3><label className="flex flex-col gap-1 text-muted">{t('settings.language')}<select value={language} onChange={(e) => setLanguage(e.target.value)}><option value="ar">العربية</option><option value="en">English</option></select></label><button>{t('settings.save')}</button></form>
        <form className="ui-card ui-stack" onSubmit={changePassword}><h3>{t('settings.changePassword')}</h3><label className="flex flex-col gap-1 text-muted">{t('settings.currentPassword')}<input type="password" required value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} /></label><label className="flex flex-col gap-1 text-muted">{t('settings.newPassword')}<input type="password" required minLength={8} value={newPassword} onChange={(e) => setNewPassword(e.target.value)} /></label><label className="flex flex-col gap-1 text-muted">{t('settings.confirmPassword')}<input type="password" required minLength={8} value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} /></label><button>{t('settings.updatePassword')}</button></form>
      </div>
    </div>
  </section>
}
