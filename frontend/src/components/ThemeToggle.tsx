import { useTranslation } from 'react-i18next'
import { useTheme } from '../theme/ThemeContext'

export default function ThemeToggle() {
  const { t } = useTranslation()
  const { theme, toggleTheme } = useTheme()

  return (
    <button type="button" onClick={toggleTheme} aria-label={t('theme.toggle')} title={t('theme.toggle')}>
      {theme === 'dark' ? '☀️' : '🌙'}
    </button>
  )
}
