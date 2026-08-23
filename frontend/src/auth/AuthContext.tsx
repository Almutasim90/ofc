import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAuthToken } from '../api/client'
import type { AuthUser, LoginResponse } from './types'

const STORAGE_KEY = 'pos.auth'

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
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function loadStored(): StoredAuth | null {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as StoredAuth
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [stored, setStored] = useState<StoredAuth | null>(() => loadStored())

  useEffect(() => {
    setAuthToken(stored?.token ?? null)
  }, [stored])

  const login = async (username: string, password: string) => {
    const response = await api.post<LoginResponse>('/api/auth/login', { username, password })
    const user: AuthUser = {
      userId: response.userId,
      fullName: response.fullName,
      branchId: response.branchId,
      roleName: response.roleName,
      preferredLanguage: response.preferredLanguage,
      permissions: response.permissions,
    }
    const next: StoredAuth = { token: response.token, user }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setStored(next)
  }

  const logout = () => {
    localStorage.removeItem(STORAGE_KEY)
    setStored(null)
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user: stored?.user ?? null,
      token: stored?.token ?? null,
      login,
      logout,
      hasPermission: (permission: string) => stored?.user.permissions.includes(permission) ?? false,
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
