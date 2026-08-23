import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { RTL_LANGUAGES } from '../i18n'

export default function LanguageSwitcher() {
  const { i18n, t } = useTranslation()

  useEffect(() => {
    const isRtl = RTL_LANGUAGES.includes(i18n.language)
    document.documentElement.dir = isRtl ? 'rtl' : 'ltr'
    document.documentElement.lang = i18n.language
  }, [i18n.language])

  const toggleLanguage = () => {
    const next = i18n.language === 'ar' ? 'en' : 'ar'
    i18n.changeLanguage(next)
  }

  return (
    <button type="button" onClick={toggleLanguage}>
      {t('language.switchTo')}
    </button>
  )
}
