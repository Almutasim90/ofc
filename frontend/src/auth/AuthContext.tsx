import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { api, AUTH_STORAGE_KEY } from '../api/client'
import i18n from '../i18n'
import { applyTheme } from '../theme/theme'
import type { AuthUser, LoginResponse } from './types'

interface StoredAuth {
  token: string
  user: AuthUser
}

interface AuthContextValue {
  user: AuthUser | null
  token: string | null
  login: (username: string, password: string) => Promise<void>
  logout: () => void
  hasPermission: (permission: string) => boolean
  updatePreferences: (preferredLanguage: string, preferredTheme: string | null) => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function loadStored(): StoredAuth | null {
  const raw = localStorage.getItem(AUTH_STORAGE_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as StoredAuth
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [stored, setStored] = useState<StoredAuth | null>(() => loadStored())

  const login = async (username: string, password: string) => {
    const response = await api.post<LoginResponse>('/api/auth/login', { username, password })
    const user: AuthUser = {
      userId: response.userId,
      fullName: response.fullName,
      branchId: response.branchId,
      roleName: response.roleName,
      preferredLanguage: response.preferredLanguage,
      preferredTheme: response.preferredTheme,
      permissions: response.permissions,
    }
    const next: StoredAuth = { token: response.token, user }
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(next))
    setStored(next)

    // Loaded automatically at login, same as PreferredLanguage: a saved
    // theme choice overrides whatever OS-preference/local toggle was active.
    i18n.changeLanguage(response.preferredLanguage)
    if (response.preferredTheme === 'light' || response.preferredTheme === 'dark') {
      applyTheme(response.preferredTheme)
    }
  }

  const logout = () => {
    localStorage.removeItem(AUTH_STORAGE_KEY)
    setStored(null)
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user: stored?.user ?? null,
      token: stored?.token ?? null,
      login,
      logout,
      hasPermission: (permission: string) => stored?.user.permissions.includes(permission) ?? false,
      updatePreferences: (preferredLanguage: string, preferredTheme: string | null) => {
        setStored((current) => {
          if (!current) return current
          const next = { ...current, user: { ...current.user, preferredLanguage, preferredTheme } }
          localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(next))
          return next
        })
      },
    }),
    [stored],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}
