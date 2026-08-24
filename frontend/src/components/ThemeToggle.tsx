import { useTranslation } from 'react-i18next'
import { useTheme } from '../theme/ThemeContext'
import AppIcon from './AppIcon'

export default function ThemeToggle() {
  const { t } = useTranslation()
  const { theme, toggleTheme } = useTheme()

  return (
    <button className="utility-icon-button" type="button" onClick={toggleTheme} aria-label={t('theme.toggle')} title={t('theme.toggle')}>
      <AppIcon className="h-5 w-5" name={theme === 'dark' ? 'sun' : 'moon'} />
    </button>
  )
}
