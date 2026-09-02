import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
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
  updatePreferences: (preferences: Partial<UserPreferences>) => Promise<void>
}

type UserPreferences = Pick<AuthUser, 'preferredLanguage' | 'preferredTheme'>

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

function restorePreferences(stored: StoredAuth | null) {
  if (!stored) return
  void i18n.changeLanguage(stored.user.preferredLanguage)
  if (stored.user.preferredTheme === 'light' || stored.user.preferredTheme === 'dark') {
    applyTheme(stored.user.preferredTheme)
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [stored, setStored] = useState<StoredAuth | null>(() => {
    const initial = loadStored()
    restorePreferences(initial)
    return initial
  })
  const storedRef = useRef(stored)
  const preferenceSave = useRef(Promise.resolve())

  const replaceStored = useCallback((next: StoredAuth | null) => {
    storedRef.current = next
    setStored(next)
    if (next) localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(next))
    else localStorage.removeItem(AUTH_STORAGE_KEY)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
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
    replaceStored(next)

    // Loaded automatically at login, same as PreferredLanguage: a saved
    // theme choice overrides whatever OS-preference/local toggle was active.
    i18n.changeLanguage(response.preferredLanguage)
    if (response.preferredTheme === 'light' || response.preferredTheme === 'dark') {
      applyTheme(response.preferredTheme)
    }
  }, [replaceStored])

  const logout = useCallback(() => {
    replaceStored(null)
  }, [replaceStored])

  const updatePreferences = useCallback((preferences: Partial<UserPreferences>) => {
    const current = storedRef.current
    if (!current) return Promise.resolve()

    const next: StoredAuth = { ...current, user: { ...current.user, ...preferences } }
    replaceStored(next)

    const save = preferenceSave.current.catch(() => {}).then(() => {
      const latest = storedRef.current?.user
      if (!latest) return
      return api.put('/api/me/preferences', {
        preferredLanguage: latest.preferredLanguage,
        preferredTheme: latest.preferredTheme,
      }).then(() => {})
    })
    preferenceSave.current = save
    return save
  }, [replaceStored])

  const value = useMemo<AuthContextValue>(
    () => ({
      user: stored?.user ?? null,
      token: stored?.token ?? null,
      login,
      logout,
      hasPermission: (permission: string) => stored?.user.permissions.includes(permission) ?? false,
      updatePreferences,
    }),
    [login, logout, stored, updatePreferences],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}
