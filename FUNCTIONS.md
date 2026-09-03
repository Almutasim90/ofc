# OFC POS — Function & Calculation Reference

> A comprehensive inventory of every function, service method, component, hook and
> business calculation across the **backend** (ASP.NET Core / EF Core) and the
> **frontend** (React + TypeScript + Vite).
>
> Generated from the current `main` branch source tree.

---

## Table of Contents

1. [Domain Constants](#1-domain-constants)
2. [Domain Events](#2-domain-events)
3. [Calculation Engines (pure/static)](#3-calculation-engines)
4. [Application Services (backend)](#4-application-services-backend)
5. [API Controllers & Hubs (backend)](#5-api-controllers--hubs-backend)
6. [Infrastructure: Seed & Background Services](#6-infrastructure-seed--background-services)
7. [Frontend: Utilities, API Client & Auth](#7-frontend-utilities-api-client--auth)
8. [Frontend: Reusable Components](#8-frontend-reusable-components)
9. [Frontend: Pages (screens)](#9-frontend-pages-screens)
10. [Frontend: Theme, Routing & Entry](#10-frontend-theme-routing--entry)

---

## 1. Domain Constants

**`backend/src/POS.Domain/Constants/`**

| Constant | Values / Purpose |
|---|---|
| `PermissionKeys` | Every permission string used by `RequirePermission` (e.g. `SalesEdit`, `OrdersCreate`, `ReportsBranchView`, `InvoiceManage`, `TablesManage`…). |
| `RoleNames` | `Cashier`, `GeneralManager`, etc. Used to scope sales/editing access. |
| `DefaultRolePermissions` | `ByRole[roleName] → permission keys[]` seed map. |
| `PaymentMethods` | `Cash`, `Card`, `Mixed`, `Debt`; `All` set used by the sale payment calculator. |
| `ShiftStatus` | `Open`, `Closed`. |
| `SaleStatus` | `Completed`, `Void`. |
| `AppClaimTypes` | JWT claim type names. |
| `BranchFeatureKeys` | `QrOrdering`, `CarPickup` (per-branch feature flags). |
| `SalesChannelIds` | Stable GUIDs for `InStore`, `QrTable`, `QrCar`. |

---

## 2. Domain Events

| Event | Purpose |
|---|---|
| `SaleCompletedEvent(saleId, branchId, cashierUserId, createdAt)` | Raised after a new sale is saved; consumed by `LowStockMonitoringService`. |
| `IDomainEvent` | Marker interface for all domain events. |

---

## 3. Calculation Engines (pure / static)

### `SalePaymentCalculator`
`backend/src/POS.Application/Sales/SalePaymentCalculator.cs`

| Function | Logic |
|---|---|
| `Calculate(method, total, cash, card)` | Resolves the cash/card split. Cash-only with no amounts → `(total,0)`; card-only → `(0,total)`; otherwise validates: known method, non-negative, ≤3 decimals (`round(cash,3)==cash`), `cash+card==total`, cash-only has `card==0`, card-only has `cash==0`, mixed requires both `>0`. Invalid → `ValidationException`. Returns `(Cash, Card)`. |

### `StockLevelCalculator`
`backend/src/POS.Application/Sales/StockLevelCalculator.cs`

| Function | Logic |
|---|---|
| `AfterSale(currentQuantity, consumedQuantity)` | `max(0, current - consumed)` — stock never goes negative. |

### `ShiftCashCalculator`
`backend/src/POS.Application/Shifts/ShiftCashCalculator.cs`

| Function | Logic |
|---|---|
| `Expected(openingCash, inStoreCashSales)` | `openingCash + inStoreCashSales`. |
| `Actual(counts)` | `Σ denomination × quantity` over `CashCountLineRequest`. |
| `Variance(actual, expected)` | `actual - expected`. |

### `ClosingScheduleCalculator`
`backend/src/POS.Application/Closing/ClosingScheduleCalculator.cs`

| Function | Logic |
|---|---|
| `GetDueUtc(shiftOpenedUtc, config, exceptions, branchId)` | Inactive config → `null`. Converts open time to Muscat local, fetches the business-date exception (branch-specific first, then global), resolves `closeTime` (exception override or default), builds `dueLocal = businessDate @ closeTime`, rolls to **next day** if `dueLocal <= openedLocal` (overnight close), returns as UTC. |

### `MuscatClock`
`backend/src/POS.Application/Closing/MuscatClock.cs`

| Function | Logic |
|---|---|
| `ToLocal(utc)` | UTC → server-local. |
| `ToUtc(local)` | Local → UTC. |

### `InventoryQuantityCalculator`
`backend/src/POS.Application/Inventory/InventoryQuantityCalculator.cs`

| Function | Logic |
|---|---|
| `FromPackages(baseQuantityPerPackage, packageCount)` | Guards `base>0`, `count>=0`; returns `round(base×count, 3)`. |

### `InvoiceService` (static helpers)
`backend/src/POS.Application/Invoices/InvoiceService.cs`

| Function | Logic |
|---|---|
| `Round(value)` | `Math.Round(value, 3, MidpointRounding.AwayFromZero)`. |
| `ApplySettings(order, settings)` | Idempotent — no-op if `InvoicePricesIncludeTax` already set. Copies legal identity, currency, tax flags, footer onto the order. |
| `CalculateOrder(order)` | **Legacy** (`InvoicePricesIncludeTax == null`): `Subtotal=Σ LineTotal`, `Discount=min(Discount,Subtotal)`, `GrandTotal=Subtotal−Discount`. **Taxed**: sets per-line net/tax/gross, then `lineTax=Σ InvoiceTaxSnapshot`, `discountTax = inclusive?0 : round(Discount×rate/100)`, `GrandTotal = inclusive ? afterDiscount : round(afterDiscount + max(0, lineTax−discountTax))`. |
| `CaptureCompletedSnapshot(order)` | Freezes subtotal/discount/tax/grand-total + `CapturedAt` once (`InvoiceSnapshotCapturedAt`). |
| `BuildDocument(order)` | Materialises an immutable `InvoiceDocument` from the order (prefers the snapshot if captured, else recomputes `Totals`). Tax lines resolved via `LineValues`. |
| `Amounts(amount, inclusive, rate)` | Inclusive: `net = rate==0 ? amount : round(amount/(1+rate/100))`, `tax=round(amount−net)`. Exclusive: `tax=round(amount×rate/100)`, gross=`amount+tax`. |
| `CreatePdf(invoice)` | Renders an A4 PDF via QuestPDF (header, table of lines, totals footer, payments). |

### `LowStockEmailTemplate`
`backend/src/POS.Application/Notifications/LowStockEmailTemplate.cs`

| Function | Logic |
|---|---|
| `SuggestedReplenishment(current, threshold)` | `max(0, threshold − current)`. |
| `Build(data)` | Renders the bilingual RTL/LTR HTML alert, HTML-encoding all values, formatting `CurrentQuantity:N3`/`Numeric` and the suggested quantity. |

### `QrTokenService`
`backend/src/POS.Application/QrOrdering/QrTokenService.cs`

| Function | Logic |
|---|---|
| `Generate(pointId, version)` | `"v1.{version}.{Base64Url(HMACSHA256(secret, "qr-point:v1:{pointId:N}:{version}"))}"`; requires ≥32-byte secret. |
| `Verify(pointId, expectedVersion, token)` | Parses `v1.v.sig`, checks version match, recomputes HMAC and compares with `FixedTimeEquals` (timing-safe). Base64Url failures → `false`. |

### `ModifierService.ValidateSelectionAsync` (selection rules)
`backend/src/POS.Application/Modifiers/ModifierService.cs`

- Enforces **no duplicate** option IDs, all options belong to an active option of a linked group, and per-group `min ≤ count ≤ max` where `min = isRequired ? max(1, MinSelect) : MinSelect`.
- Returns `(totalDelta = Σ PriceDelta, selected options)`.

---

## 4. Application Services (backend)

Each service lives in `backend/src/POS.Application/**`.

### Auth
**`AuthService`**
- `LoginAsync(LoginRequest)` → validates username/password via `IPasswordHasher`, returns `LoginResponse` (JWT + user profile + permissions).

### Users
**`UserService`**
- `CreateAsync`, `UpdateAsync`, `UpdateMyPreferencesAsync`, `ChangeMyPasswordAsync`, `SetPermissionOverrideAsync`.
- `PermissionResolver` maps a user → effective permission set (role defaults + overrides).

### Branches / Catalog (legacy product POS)
**`BranchService`**: `CreateAsync`, `UpdateAsync`.
**`ProductService`**: `CreateAsync`, `UpdateAsync`.
**`RawMaterialService`**: `CreateAsync`, `UpdateAsync`.
**`RecipeService`**: `SetAsync(request)` — replaces recipe lines for a product+branch.
**`StockService`**:
- `GetStatusAsync(branchId)` → per-material current quantity/threshold/`isLowStock`/package.
- `AdjustAsync(request, userId)` — delta-adds stock, logs a `StockAdjustment`.
- `SetLowStockThresholdAsync(request)`.
- `CreateSupplyPackageAsync(request)` — one package per material.
- `ReceiveAsync(request, userId)` — `quantity = FromPackages(baseQuantity, packageCount)`; writes receipt + adjustment.
- `GetRecentReceiptsAsync(branchId)`.
- `CreateInventoryItemAsync(request, userId)` — builds material + package + opening stock in one call; rejects duplicate names.

### Channels
**`ChannelService`**: `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `SetPricesAsync`, `GetAvailabilityAsync`, `SetAvailabilityAsync`.

### Closing Schedule
**`ClosingScheduleService`**: `GetConfigAsync`, `UpdateConfigAsync`, `CreateExceptionAsync`, `UpdateExceptionAsync`, `DeleteExceptionAsync`, `GetUpcomingAsync`.

### Cash Shifts
**`CashShiftService`**
- `GetAsync(branchId)` — last 100 shifts.
- `OpenAsync(request)` — guards negative float, duplicate open shift, permission.
- `CloseAsync(id, request)` — `cash = Σ CASH payments`, `counted = Σ note×qty`, `ExpectedCash = OpeningFloat + cash`, `VarianceCash = counted − ExpectedCash`; writes denomination rows.
- `ValidateCounts` — static validator.

### Shifts (legacy)
**`ShiftService`**: `GetCurrentAsync`, `GetLatestClosedAsync`, `OpenAsync`, `CloseAsync`.
**`VoidService`**: `VoidAsync(request)`.

### Sales
**`SaleService`** — see also `ApplyDiscount` below.
- `CreateAsync` / `UpdateAsync` → shared `SaveAsync(request, existing?, reason?)`.
- `ListAsync(branchId)`, `HistoryAsync(id)`, `ListForShiftAsync(shiftId)`.
- Discount calc (`ApplyDiscount`):
  - `None → 0`, `Percentage → subtotal×value/100`, `FixedAmount → value`.
  - `max(0, round(subtotal − min(subtotal, amount), 3))`.
- Totals: `TotalAmount = ApplyDiscount(Σ line totals, order discount)`, `DiscountAmount = rawTotal − TotalAmount`.
- Inventory: aggregates recipe requirements by material, decrements stock capped at availability + prior consumption, records `SaleInventoryConsumption`.
- Concurrency: `shift.SalesRevision++`, `sale.Revision++` on edit; edits stored as `SaleEdit` (before/after JSON).
- Emits `SaleCompletedEvent` on create.

### Payments & Bill Split (restaurant)
**`OrderPaymentService`**
- `RecordAsync(orderId, request)` — validates positive amount, order not cancelled/closed, split must be chosen if splits exist, split balance, method availability, approval permission, remaining balance, open cash shift for `CASH`. On final payment marks order `Paid` (if no session **or** status is `Sent`), captures invoice snapshot, `PaymentRevision++`.
- `EditAsync(orderId, request)` — price override on a paid/closed order; forbids total `< payments or `< split allocations registered; writes `OrderEditLog`.

**`BillSplitService`**
- `CreateEqualAsync(orderId, request)` — `base = floor(remaining×1000 / shares)/1000`; last share absorbs remainder (`remaining − base×(shares−1)`) so the sum is exact.
- `CreateItemAsync(orderId, request)` — validates quantities, computes `amount = allocatesAllItems ? remaining : round(GrandTotal × selectedValue / activeValue, 3)` (pro-rata across active item value).
- `ListAsync(orderId)` — split + paid + remaining (`Amount − Σ payments`).

### Orders (restaurant)
**`RestaurantOrderService`**
- `CreateAsync(request)` — validates lines/table/car-pickup, resolves channel + invoice settings, applies `InvoiceService.CalculateOrder`, claims next order number.
- `GetByIdAsync(orderId)`, `GetBranchIdAsync`, `GetOrderingSessionIdAsync`, `GetAsync(branchId)` (last 100), `ToDto`, `ValidateCarPickup`.

**`OrderCancellationService`**
- `CancelItemAsync(orderId, itemId, reason)`, `CancelOrderAsync(orderId, reason)` — recalc totals via `InvoiceService.CalculateOrder`, auto-cancel order when all items cancelled, log `OrderCancellation`, run `StageReversal` for stock.

### Printers / Printing
**`PrinterAdminService`**: `SaveConfigAsync`, `SaveSectionAsync`, `TestAsync`.
**`OrderPrintingService`**
- `ConfirmAndPrintAsync(orderId)` / `ConfirmQrAndPrintAsync(orderId, branchId)` → `ConfirmAndPrintAsync` (statuses `Open` or approved-QR), builds jobs via `BuildJobs`, delegates stock to `RestaurantInventoryService.Confirm`, sends each via `IRawPrinterClient`.
- `PrintCustomerInvoiceAsync(orderId)` — prints `BuildCustomerReceipt(BuildDocument(order))`.
- `BuildJobs(order, fallback)` — groups active items by printer section, one kitchen job per section + one receipt job.
- `BuildCustomerReceipt(invoice)` — ESC/POS text layout (header, tax reg, disclaimer, item/qty/net/tax/gross, sub/discount/tax/grand total, payments, footer), UTF-8.
- `EscPosDocument.Text(text)` — `0x1B 0x40` init + bytes + `0x1D 0x56 0x00` cut.

### Modifiers
**`ModifierService`**: `GetAsync(menuItemId?)`, `SaveAsync(id?, request)`, `DeleteAsync(id)`, `ValidateSelectionAsync`.

### Restaurant Catalog
**`RestaurantCatalogService`**
- Tables/flags/categories/items CRUD + `ReorderCategoriesAsync`, `SetCategoryAvailabilityAsync`, `SaveComboAsync`.

### Restaurant Inventory
**`RestaurantInventoryService`**
- `Units()`/`Ingredients()`/`Warehouses(branchId)`/`Reasons()`/`Stock(warehouseId)`.
- `SaveUnit`, `SaveIngredient`, `SaveWarehouse` (de-defaults prior default), `Move`, `SaveRecipe`.
- `Confirm(orderId, capabilityBranchId?, qrConfirmation)` — computes required ingredients (items + combo selections), validates available stock, decrements, writes negative `RestaurantInventoryTransaction` (reason `THEORETICAL_CONSUMPTION`), sets order `Sent`. Idempotent for QR when already `Sent` (skips if a transaction exists).
- `StageReversal(orderId, itemId?, ct)` — returns consumed quantities (reason `CANCELLATION_RETURN`).

**`StockCountService`**
- `Start(branchId, warehouseId)` — seeds lines from system stock; rejects an existing draft.
- `GetDraft`, `Get`, `Save` (sets counted + `variance = counted − system`).
- `Finalize` — re-checks stock unchanged (else `ConflictException`), writes `INVENTORY_COUNT_ADJUSTMENT` transactions and sets stock to counted.

### Qr Ordering
**`QrOrderingService`** — full lifecycle:
- `GetBaysAsync`, `GetPointsAsync`, `SaveBayAsync`, `SavePointAsync`, `RegenerateAsync` (bumps token version, invalidates old links).
- `ResolveAsync(token)` / `ResolveSignedAsync(pointId, signedToken)` → resolving an ordering point into (or reusing) an open `OrderingSession`; creates/reuses session access token.
- `GetMenuAsync(sessionId, accessToken)` → QR menu (active categories minus disabled-for-branch, minus empty categories; items with groups/options/combos).
- `SchedulesAsync` / `SaveScheduleAsync` (weekly, overnight-crossing windows).
- `AddAsync(sessionId, request)` — appends lines to the session's open order (or creates it), applies `InvoiceService.CalculateOrder`; guarded by per-session `SemaphoreSlim`.
- `ConfirmAsync(orderId, request)` — `Open → PendingApproval`, sets `SubmittedAt`; requires prepayment if channel requires it.
- `ApproveAsync(orderId)` — gated by `ApprovalLocks`; requires `PendingApproval`, sets `ApprovedAt/By`, prints + deducts stock (via `ConfirmQrAndPrintAsync`), transitions to `Paid` if fully paid and captures snapshot. Idempotent for `Sent`/`Paid`.
- `RejectAsync(orderId, reason)` — cancels lines, sets rejection, closes the session; refuses if payments exist.
- `EditPendingAsync(orderId, request)` — replaces lines, recalculates, rejects totals below recorded payments, `PaymentRevision++`, logs `QrEdited`.
- `GetSessionOrderAsync(sessionId, accessToken)` — status read (auth-only, does not require branch open).
- `CloseAsync(sessionId)` — requires full settlement, closes session.
- `TransferOrderAsync(orderId, newOrderingPointId, userId, notes)` — moves order to another table/car point, updates type/table/car, logs `Transferred`.
- `ValidateSessionAsync` — membership check only.

### Invoices
**`InvoiceService`** — see table in §3 (static calc) + `GetDocumentAsync(orderId)`.
**`InvoiceSettingsService`**: `GetAsync(branchId)` (branch scoped), `SaveAsync(request)`.

### Reports
**`ReportService`**
- `GetDashboardAsync(from, to, branchId?)` — daily trend, per-branch, payment breakdown, product sales (with cash/card splits), cash-shift variances, order edits, order-type sales. Computes `avgInvoice = total / invoices`.
- `GetDiscountsAsync(...)`, `GetChannelDistributionAsync(...)`, `GetDailyBranchAsync(...)`, `GetGlobalAsync(date)`, `GetShiftInventoryAsync(shiftId)`.
- Only `Paid`/`Closed` orders count (`ReportableStatuses`); period ≤ 366 days; branch scope enforced.

### AI / Settings
**`AiInsightService`**: `GetSettingsAsync`, `SaveSettingsAsync`, `GenerateAsync`, `TestConnectionAsync`.
**`EmailSettingsService`**: `GetAsync`, `SaveAsync`, `SendTestAsync`.
**`ReceiptSettingsService`**: `GetAsync`, `SaveAsync`.
**`NotificationService`**: lists/ACKs low-stock notifications.

### Table Management
**`TableManagementService`**
- `SaveFloorAsync`, `DeleteFloorAsync` (refuses non-empty floor), `SaveTableAsync` (layout), board with live occupancy from open/sent orders + QR sessions.

---

## 5. API Controllers & Hubs (backend)

Thin REST delegates under `backend/src/POS.API/Controllers/` that call the Application
services above. Standard `[Authorize]` + `RequirePermission` on mutating endpoints.
`Program.cs` maps:
- `/healthz`, `/api/healthz` → DB-connectivity readiness (503 if unreachable).
- `/hubs/restaurant-orders`, `/hubs/qr-orders` (SignalR; auth via `access_token` query for the staff hub).

Controllers: `Auth`, `Me`, `Users`, `Roles`, `Permissions`, `Branches`, `Products`,
`RawMaterials`, `Inventory`, `Channels`, `Sales`, `Shifts`, `CashShifts`, `Voids`,
`ClosingSchedule`, `EmailSettings`, `ReceiptSettings`, `Ai`, `Notifications`,
`Modifiers`, `RestaurantCatalog`, `RestaurantOrders`, `OrderPayments`,
`OrderTransfers`, `OrderingPoints`, `Tables`, `RestaurantInventory`, `StockCounts`,
`Printers`, `InvoiceSettings`, `Reports`, `Uploads`.

**Hubs**
- `RestaurantOrdersHub.JoinBranch(branchId)` — requires `OrdersCreate` permission; adds connection to `branch:{id}` group.
- `QrOrdersHub.JoinSession(sessionId, accessToken)` — validates the session; joins `qr-session:{id}` group.

---

## 6. Infrastructure: Seed & Background Services

### `SeedData.SeedAsync(db, passwordHasher)`
`backend/src/POS.Infrastructure/Persistence/Seed/SeedData.cs`
- Seeds permissions, sales channels (`IN_STORE`, `QR_TABLE`, `QR_CAR`), branch feature flags (`QR_ORDERING`) and QR channel availabilities per existing branch, roles, role-permissions, and a bootstrap admin (`admin` / `Admin@12345`).
- Calls `SeedDemoRestaurantDataAsync` — an **idempotent, per-entity** demo seeder:
  - Units: `Kilogram`, `Liter`, `Piece`.
  - Ingredients (resolved by English name): Beef, Burger bun, Potato, Chicken, Rice, Tomato, Cheese, Cola syrup, Ice.
  - Categories: Starters, Mains, Drinks, Desserts.
  - Items: Beef burger, French fries, Chicken shawarma, Chicken w/ rice, Cola, Water, Ice cream + Family combo (Main + Drink slots).
  - Addon groups: Size, Extras, Sauce, Ice level (with options/deltas) wired to items.
  - Recipes (item → ingredient qty), a default warehouse with stock, a floor + 5 tables.
  - Each record is guarded by a name/key existence check so it never duplicates.

### Background / hosted services
- `AutomaticShiftClosingService` — closes overdue shifts via `ClosingScheduleCalculator`.
- `LowStockMonitoringService` — raises/resolves low-stock notifications, dispatches `LowStockEmailTemplate` alerts.
- `CurrentUserService`, `JwtTokenService`, `PasswordHasherService`, `TcpRawPrinterClient`, `SupabaseStorageService`, `DomainEventPublisher`, `DatabaseEmailNotificationSender` (infrastructure implementations of Application abstractions).

---

## 7. Frontend: Utilities, API Client & Auth

### `src/utils/payments.ts`
| Function | Logic |
|---|---|
| `roundMoney(value)` | Mirrors backend `round(x,3, midpoint-to-even)`: `scaled=value×1000`, picks even `lower` on a `.5` tie, else `Math.round`. |
| `paymentAmounts(method, total, cash)` | Cash → `(total,0)`; Card → `(0,total)`; Mixed → `(cash, roundMoney(total−cash))`. |
| `cashFromSplitInput(field, value, total)` | Validates `0≤amount≤total` and 3-decimal precision; returns the companion split field value. |
| `validPayment(method, total, cash)` | True when amounts are finite/non-negative, 3-decimal exact, and mixed requires both parts `>0`. |

### `src/api/client.ts`
- `apiEndpoint(path)` — origin-aware URL builder.
- `resolveApiAssetUrl(url)` — rewrites `/api/uploads`/`/uploads` to the API origin.
- `class ApiError` — carries `status`.
- `getStoredToken()` — reads the JWT from `localStorage` on every request.
- `api.get/post/put/upload/delete` — thin fetch wrappers, auto JSON + bearer header.

### `src/auth/AuthContext.tsx`
- Auth provider exposing `user`, `token`, `hasPermission`, `login`, `logout`, persistence of language/theme preferences.

---

## 8. Frontend: Reusable Components

| Component | Responsibility |
|---|---|
| `DataTable` | Generic table with debounced search, stable sorting, pagination, URL-state persistence, loading/empty states, accessibility (`aria-sort`, `aria-busy`). |
| `Money` | Renders a value `toFixed(3)` + Omani Rial symbol. |
| `AppIcon` | SVG icon registry (`cashier`, `shift`, `reports`, `inventory`, `products`, `materials`, `branches`, `users`, `schedule`, `channels`, `ai`, `notifications`, `settings`, `logout`, `more`, `chevron`, `close`, `plus`, `minus`, `trash`, `sun`, `moon`, `home`, `email`, `printer`, `check`, `alert`, `fullscreen`, `fullscreenExit`, `table`, `qrcode`). |
| `Layout` | Shell: collapsible sidebar (grouped nav), top bar, breadcrumb, bottom mobile nav, kiosk/fullscreen mode, theme/lang switchers, notification bell. |
| `Breadcrumb` | Trail rendering for nested routes. |
| `BottomSheet` | Mobile bottom drawer for the "More" nav. |
| `ToastContext` | Toast notifications (`useToast`). |
| `LanguageSwitcher` / `ThemeToggle` | i18n + theme. |
| `NotificationBell` | Pending low-stock notification dropdown. |
| `PaymentSelector` | Cash/Card/Mixed picker. |
| `Receipt` | Legacy receipt rendering. |
| `SavedOrders` | Reopen saved sales. |
| `PasswordField` | Password input with reveal toggle. |
| `TableTools` | `SearchBox` shared control. |

---

## 9. Frontend: Pages (screens)

Each page is a screen under `src/pages/`; all use `api.*` for data and `DataTable`/cards for presentation.

| Page | Purpose |
|---|---|
| `CashierPage` | Point-of-sale checkout (categories, product grid, cart, channel prices, shift gate). |
| `LoginPage` | Authentication. |
| `RestaurantOrdersPage` | Create/print restaurant orders, approve/reject/edit QR (Action + `.PendingApproval`), SignalR live updates from the staff hub. |
| `OrderPaymentsPage` | Record payments, create/equal bill splits, invoice edits, payment history. |
| `OrderCancellationsPage` | Cancel order/item with reason + log. |
| `OrderTransfersPage` | Move an order to another table/car point. |
| `CashShiftsPage` | Open/close cash shifts, denomination counts, variance. |
| `ShiftPage` | Legacy shift open/close/count. |
| `ReportsPage` | Dashboards, sales, discounts, channels, cash variances, order edits. |
| `AiInsightsPage` / `AiSettingsPage` | AI-generated insight + provider config. |
| `InventoryPage` | Legacy inventory: stock status, adjustments, packages, receipts. |
| `RestaurantInventoryPage` | Restaurant ingredient/warehouse/recipe management + stock movements. |
| `StockCountsPage` | Draft/finalize physical stock counts, variance. |
| `ProductsPage` / `ProductRecipePage` / `RawMaterialsPage` | Legacy catalog + recipes. |
| `RestaurantCatalogPage` | Menu categories/items/combos + per-branch availability. |
| `ModifiersPage` | Addon groups/options + item wiring. |
| `ChannelsPage` | Sales channels + pricing + branch availability. |
| `BranchesPage` | Branch CRUD. |
| `UsersPage` / `UserPermissionsPage` | Users + permission overrides. |
| `PrintersPage` | Printer configs/sections/route products/test. |
| `TablesPage` | Floor & table layout board (drag-and-drop positioning, click-to-place add, bulk add, occupancy from live orders/QR sessions). |
| `OrderingPointsPage` | QR points, car bays, secure QR regeneration, weekly QR schedule. |
| `QrLandingPage` | Customer QR menu, cart, configure modifiers/combos, submit (Open → PendingApproval), live status via `QrOrdersHub` + polling, retry confirmation. |
| `ClosingSchedulePage` | Auto-close config + exceptions. |
| `InvoiceSettingsPage` | Tax-invoice legal identity/tax config. |
| `EmailSettingsPage` / `ReceiptSettingsPage` | SMTP + receipt settings. |
| `NotificationsPage` | Low-stock notification list. |
| `SettingsPage` | User preferences hub. |

---

## 10. Frontend: Theme, Routing & Entry

- `main.tsx` — mounts the app, loads i18n.
- `App.tsx` — React Router route table (lazily loads pages; protected routes via `ProtectedRoute`; `HomeRedirect`).
- `i18n.ts` — configurable locale (`ar`/`en`) + resources in `src/locales/`.
- `theme/theme.ts`, `theme/ThemeContext.tsx` — theme tokens + persistence.
