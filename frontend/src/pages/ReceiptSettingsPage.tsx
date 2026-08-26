import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../api/client'

interface ReceiptSettingsDto { headerText: string | null }

export default function ReceiptSettingsPage() {
  const { t } = useTranslation()
  const [headerText, setHeaderText] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.get<ReceiptSettingsDto>('/api/receipt-settings')
      .then((x) => setHeaderText(x.headerText ?? ''))
      .catch((err) => setError(err instanceof ApiError ? err.message : t('receipt.loadError')))
  }, [t])

  const save = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true); setError(null); setMessage(null)
    try {
      const x = await api.put<ReceiptSettingsDto>('/api/receipt-settings', { headerText })
      setHeaderText(x.headerText ?? '')
      setMessage(t('receipt.saved'))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('receipt.saveError'))
    } finally {
      setBusy(false)
    }
  }

  return <section>
    <h1>{t('receipt.settingsTitle')}</h1>
    <p>{t('receipt.settingsDescription')}</p>
    {message && <p className="text-primary">{message}</p>}
    {error && <p className="error-text" role="alert">{error}</p>}
    <form className="ui-card ui-stack" onSubmit={save}>
      <label className="flex flex-col gap-1 text-muted">
        {t('receipt.headerLabel')}
        <textarea rows={4} maxLength={500} placeholder={t('receipt.headerPlaceholder')} value={headerText} onChange={(e) => setHeaderText(e.target.value)} />
        <small>{t('receipt.headerHint')}</small>
      </label>
      <button disabled={busy}>{busy ? t('common.loading') : t('common.save')}</button>
    </form>
  </section>
}
