export interface AuthUser {
  userId: string
  fullName: string
  branchId: string | null
  roleName: string
  preferredLanguage: string
  preferredTheme: string | null
  permissions: string[]
}

export interface LoginResponse {
  token: string
  userId: string
  fullName: string
  branchId: string | null
  roleName: string
  preferredLanguage: string
  preferredTheme: string | null
  permissions: string[]
}
