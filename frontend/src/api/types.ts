export interface UserDto {
  id: string
  fullName: string
  username: string
  branchId: string | null
  roleId: string
  roleName: string
  preferredLanguage: string
  isActive: boolean
  createdAt: string
}

export interface RoleDto {
  id: string
  name: string
  description: string | null
}

export interface PermissionDto {
  id: string
  key: string
  description: string | null
}

export interface PermissionOverrideDto {
  permissionId: string
  permissionKey: string
  isGranted: boolean | null
}

export interface CreateUserRequest {
  fullName: string
  username: string
  password: string
  branchId: string | null
  roleId: string
  preferredLanguage: string
}

export interface UpdateUserRequest {
  fullName: string
  branchId: string | null
  roleId: string
  preferredLanguage: string
  isActive: boolean
}
