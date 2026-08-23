import { createContext, useContext, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { api, AUTH_STORAGE_KEY } from '../api/client'
import { applyTheme, getCurrentTheme, type Theme } from './theme'

interface ThemeContextValue {
  theme: Theme
  toggleTheme: () => void
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined)

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() => getCurrentTheme())
  const { i18n } = useTranslation()

  const toggleTheme = () => {
    const next: Theme = theme === 'dark' ? 'light' : 'dark'
    applyTheme(next)
    setThemeState(next)

    if (localStorage.getItem(AUTH_STORAGE_KEY)) {
      api
        .put('/api/me/preferences', { preferredLanguage: i18n.language, preferredTheme: next })
        .catch(() => {})
    }
  }

  return <ThemeContext.Provider value={{ theme, toggleTheme }}>{children}</ThemeContext.Provider>
}

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme must be used within ThemeProvider')
  return context
}
