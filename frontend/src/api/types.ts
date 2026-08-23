export interface UserDto {
  id: string
  fullName: string
  username: string
  branchId: string | null
  roleId: string
  roleName: string
  preferredLanguage: string
  preferredTheme: string | null
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

export interface BranchDto {
  id: string
  nameAr: string
  nameEn: string
  code: string
  isActive: boolean
}

export interface CreateBranchRequest {
  nameAr: string
  nameEn: string
  code: string
}

export interface UpdateBranchRequest {
  nameAr: string
  nameEn: string
  code: string
  isActive: boolean
}

export interface ProductDto {
  id: string
  nameAr: string
  nameEn: string
  category: string
  price: number
  iconOrImageUrl: string | null
  isActive: boolean
}

export interface CreateProductRequest {
  nameAr: string
  nameEn: string
  category: string
  price: number
  iconOrImageUrl: string | null
}

export interface UpdateProductRequest extends CreateProductRequest {
  isActive: boolean
}

export interface RawMaterialDto {
  id: string
  nameAr: string
  nameEn: string
  unit: string
}

export interface CreateRawMaterialRequest {
  nameAr: string
  nameEn: string
  unit: string
}

export type UpdateRawMaterialRequest = CreateRawMaterialRequest

export interface RecipeLineDto {
  rawMaterialId: string
  rawMaterialNameAr: string
  rawMaterialNameEn: string
  unit: string
  quantityRequired: number
}

export interface SetRecipeRequest {
  branchId: string
  lines: { rawMaterialId: string; quantityRequired: number }[]
}

export interface StockStatusDto {
  rawMaterialId: string
  nameAr: string
  nameEn: string
  unit: string
  currentQuantity: number
  lowStockThreshold: number
  isLowStock: boolean
}

export interface AdjustStockRequest {
  branchId: string
  rawMaterialId: string
  quantityChange: number
  reason: string
}

export interface SetLowStockThresholdRequest {
  branchId: string
  rawMaterialId: string
  threshold: number
}

export interface SaleLineRequest {
  productId: string
  quantity: number
}

export interface CreateSaleRequest {
  branchId: string
  paymentMethod: 'Cash' | 'Card'
  lines: SaleLineRequest[]
}

export interface SaleItemDto {
  productId: string
  productNameSnapshot: string
  unitPriceSnapshot: number
  quantity: number
  lineTotal: number
}

export interface SaleDto {
  id: string
  branchId: string
  shiftId: string
  cashierUserId: string
  businessDate: string
  createdAt: string
  totalAmount: number
  paymentMethod: string
  status: string
  items: SaleItemDto[]
}

export interface ShiftDto {
  id: string
  branchId: string
  cashierUserId: string
  openingCash: number
  closingCashExpected: number
  closingCashActual: number | null
  varianceAmount: number | null
  openedAt: string
  closedAt: string | null
  status: 'Open' | 'Closed'
  autoClosed: boolean
  cashSalesTotal: number
}

export interface VoidRequestDto {
  id: string
  saleId: string
  requestedByUserId: string
  reason: string
  approvedByUserId: string | null
  createdAt: string
}

export interface ClosingScheduleConfigDto {
  id: string
  defaultCloseTime: string
  isActive: boolean
}

export interface ClosingScheduleExceptionDto {
  id: string
  date: string
  overrideCloseTime: string
  branchId: string | null
  reason: string
}

export interface UpcomingClosingDto {
  scheduledCloseAt: string
  minutesRemaining: number
  warning: boolean
  scheduleActive: boolean
}
