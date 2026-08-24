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
  const cards=[
    {to:'/closing-schedule',title:t('nav.closingSchedule'),summary:summaries.closing??t('settingsHub.closingSummary'),permission:'closing.configure'},
    {to:'/users',title:t('nav.users'),summary:summaries.users??t('settingsHub.usersSummary'),permission:'users.manage'},
    {to:'/branches',title:t('nav.branches'),summary:summaries.branches??t('settingsHub.branchesSummary'),permission:'branches.manage'},
    {to:'/channels',title:t('nav.channels'),summary:summaries.channels??t('settingsHub.channelsSummary'),permission:'channels.manage'},
    {to:'/ai-settings',title:t('nav.aiSettings'),summary:summaries.ai??t('settingsHub.aiSummary'),permission:'ai.manage'},
  ].filter(c=>hasPermission(c.permission)&&`${c.title} ${c.summary}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()))
  return <section><h1>{t('settings.title')}</h1>{message && <p className="mt-3 text-primary">{message}</p>}{error && <p className="error-text">{error}</p>}<div className="mt-5 grid gap-5 lg:grid-cols-2">
    <form className="rounded-xl border border-border bg-surface p-5" onSubmit={savePreferences}><h2>{t('settings.preferences')}</h2><label className="mt-4 flex flex-col gap-1 text-muted">{t('settings.language')}<select value={language} onChange={(e) => setLanguage(e.target.value)}><option value="ar">العربية</option><option value="en">English</option></select></label><button className="mt-4">{t('settings.save')}</button></form>
    <form className="rounded-xl border border-border bg-surface p-5" onSubmit={changePassword}><h2>{t('settings.changePassword')}</h2><label className="mt-4 flex flex-col gap-1 text-muted">{t('settings.currentPassword')}<input type="password" required value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} /></label><label className="mt-3 flex flex-col gap-1 text-muted">{t('settings.newPassword')}<input type="password" required minLength={8} value={newPassword} onChange={(e) => setNewPassword(e.target.value)} /></label><label className="mt-3 flex flex-col gap-1 text-muted">{t('settings.confirmPassword')}<input type="password" required minLength={8} value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} /></label><button className="mt-4">{t('settings.updatePassword')}</button></form>
  </div><div className="mt-6"><input className="w-full max-w-md" placeholder={t('settingsHub.search')} value={search} onChange={e=>setSearch(e.target.value)}/><div className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{cards.map(c=><Link key={c.to} to={c.to} className="rounded-xl border border-border bg-surface p-5"><h2>{c.title}</h2><p>{c.summary}</p></Link>)}</div></div></section>
}
