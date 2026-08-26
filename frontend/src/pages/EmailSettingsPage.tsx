import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'

interface EmailSettingsDto { smtpHost:string; smtpPort:number; useSsl:boolean; username:string; hasPassword:boolean; fromEmail:string; fromName:string; recipients:string; isActive:boolean }

export default function EmailSettingsPage() {
  const { t } = useTranslation()
  const [form, setForm] = useState({ smtpHost:'', smtpPort:587, useSsl:true, username:'', password:'', fromEmail:'', fromName:'', recipients:'', isActive:false })
  const [hasPassword, setHasPassword] = useState(false)
  const [testRecipient, setTestRecipient] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string|null>(null)
  const [error, setError] = useState<string|null>(null)

  useEffect(() => { api.get<EmailSettingsDto>('/api/email-settings').then(x => {
    setForm({ smtpHost:x.smtpHost, smtpPort:x.smtpPort, useSsl:x.useSsl, username:x.username, password:'', fromEmail:x.fromEmail, fromName:x.fromName, recipients:x.recipients, isActive:x.isActive })
    setHasPassword(x.hasPassword)
    setTestRecipient(x.fromEmail)
  }).catch(err => setError(err instanceof ApiError ? err.message : t('email.loadError'))) }, [t])

  const save = async (event: FormEvent) => { event.preventDefault(); setBusy(true); setError(null); setMessage(null); try {
    const x = await api.put<EmailSettingsDto>('/api/email-settings', form)
    setHasPassword(x.hasPassword); setForm(current => ({...current, password:''})); setMessage(t('email.saved'))
  } catch(err) { setError(err instanceof ApiError ? err.message : t('email.saveError')) } finally { setBusy(false) } }
  const test = async () => { setBusy(true); setError(null); setMessage(null); try { await api.post('/api/email-settings/test', {recipient:testRecipient}); setMessage(t('email.testSent')) } catch(err) { setError(err instanceof ApiError ? err.message : t('email.testError')) } finally { setBusy(false) } }

  return <section><h1>{t('email.title')}</h1><p>{t('email.description')}</p>{message&&<p className="text-primary">{message}</p>}{error&&<p className="error-text" role="alert">{error}</p>}
    <form className="ui-card ui-stack" onSubmit={save}>
      <label className="checkbox-field"><input type="checkbox" checked={form.isActive} onChange={e=>setForm({...form,isActive:e.target.checked})}/><span>{t('email.active')}</span></label>
      <div className="settings-form-grid">
        <label>{t('email.smtpHost')}<input required={form.isActive} value={form.smtpHost} onChange={e=>setForm({...form,smtpHost:e.target.value})}/></label>
        <label>{t('email.smtpPort')}<input type="number" min="1" max="65535" required value={form.smtpPort} onChange={e=>setForm({...form,smtpPort:Number(e.target.value)})}/></label>
        <label>{t('email.username')}<input value={form.username} onChange={e=>setForm({...form,username:e.target.value})}/></label>
        <label>{t('email.password')}<input type="password" value={form.password} placeholder={hasPassword?'••••••••':''} onChange={e=>setForm({...form,password:e.target.value})}/></label>
        <label>{t('email.fromEmail')}<input type="email" required={form.isActive} value={form.fromEmail} onChange={e=>setForm({...form,fromEmail:e.target.value})}/></label>
        <label>{t('email.fromName')}<input value={form.fromName} onChange={e=>setForm({...form,fromName:e.target.value})}/></label>
      </div>
      <label>{t('email.recipients')}<textarea rows={3} required={form.isActive} value={form.recipients} onChange={e=>setForm({...form,recipients:e.target.value})}/><small>{t('email.recipientsHint')}</small></label>
      <label className="checkbox-field"><input type="checkbox" checked={form.useSsl} onChange={e=>setForm({...form,useSsl:e.target.checked})}/><span>{t('email.useSsl')}</span></label>
      <button disabled={busy}>{t('common.save')}</button>
    </form>
    <div className="ui-card ui-stack max-w-md"><h2>{t('email.testTitle')}</h2><label>{t('email.testRecipient')}<input type="email" value={testRecipient} onChange={e=>setTestRecipient(e.target.value)}/></label><button type="button" disabled={busy||!testRecipient||!form.isActive} onClick={test}>{t('email.sendTest')}</button></div>
  </section>
}
