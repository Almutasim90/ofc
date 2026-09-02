import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { applyTheme, getCurrentTheme, type Theme } from './theme'

interface ThemeContextValue {
  theme: Theme
  toggleTheme: () => void
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined)

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() => getCurrentTheme())
  const { user, updatePreferences } = useAuth()

  useEffect(() => {
    if (user?.preferredTheme !== 'light' && user?.preferredTheme !== 'dark') return
    applyTheme(user.preferredTheme)
    setThemeState(user.preferredTheme)
  }, [user?.preferredTheme])

  const toggleTheme = () => {
    const next: Theme = theme === 'dark' ? 'light' : 'dark'
    applyTheme(next)
    setThemeState(next)
    if (user) void updatePreferences({ preferredTheme: next }).catch(() => {})
  }

  return <ThemeContext.Provider value={{ theme, toggleTheme }}>{children}</ThemeContext.Provider>
}

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme must be used within ThemeProvider')
  return context
}
