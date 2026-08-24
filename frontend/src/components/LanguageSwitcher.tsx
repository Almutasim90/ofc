import { useTranslation } from 'react-i18next'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

function FlagIcon({ language }: { language: 'ar' | 'en' }) {
  if (language === 'ar') {
    return <svg viewBox="0 0 30 30" className="h-full w-full" aria-hidden="true"><rect width="30" height="30" fill="#fff"/><rect y="10" width="30" height="10" fill="#db242f"/><rect y="20" width="30" height="10" fill="#178b55"/><rect width="9" height="30" fill="#db242f"/><path d="M2.5 3.5l4 4m0-4l-4 4M4.5 2v7" stroke="#fff" strokeWidth="1"/></svg>
  }
  return <svg viewBox="0 0 30 30" className="h-full w-full" aria-hidden="true"><rect width="30" height="30" fill="#173f8a"/><path d="M0 0l30 30M30 0L0 30" stroke="#fff" strokeWidth="6"/><path d="M0 0l30 30M30 0L0 30" stroke="#c8102e" strokeWidth="2.5"/><path d="M15 0v30M0 15h30" stroke="#fff" strokeWidth="10"/><path d="M15 0v30M0 15h30" stroke="#c8102e" strokeWidth="5"/></svg>
}

export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation()
  const { user, updatePreferences } = useAuth()

  const toggleLanguage = async () => {
    const next = i18n.language === 'ar' ? 'en' : 'ar'
    await i18n.changeLanguage(next)
    if (user) {
      updatePreferences(next, user.preferredTheme)
      api.put('/api/me/preferences', { preferredLanguage: next, preferredTheme: user.preferredTheme }).catch(() => {})
    }
  }

  return (
    <button className="language-switcher utility-icon-button" type="button" onClick={toggleLanguage} aria-label={t('language.switchTo')} title={t('language.switchTo')}>
      <span className="flag-circle"><FlagIcon language={i18n.language === 'ar' ? 'en' : 'ar'} /></span>
    </button>
  )
}
