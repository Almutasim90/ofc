# OFC-System-Detailed-Spec.md — الملف المرجعي الأساسي (يُكمِّل OFC-Development-Brief.md)

> **دور هذا المستند:** `OFC-Development-Brief.md` يشير مرارًا إلى هذا الملف كأساس ("القسم 2 بالملف المرجعي"، "الدوال 3.1 إلى 3.7"، "Sprints 0-5, 8, 9, 10, 12 كما بالملف الأصلي") لكنه غير موجود بالمستودع. هذا المستند يسدّ تلك الفجوة: يعرّف نموذج البيانات الأساسي والدوال الجوهرية وخطة السبرنتات الأصلية التي يبني عليها الـ Brief إضافاته. **لا يُنفَّذ شيء من هذا الملف قبل موافقتك على القرارات المعلّقة بالقسم 8 بالأسفل**، وتحديدًا قرار الترحيل (Migration) الموصوف هناك — فهو يمس بيانات تشغيلية حقيقية.

---

## 0. علاقة هذا المستند بالكود الحالي

نظام `ofc` الحالي (حتى تاريخ كتابة هذا المستند) هو تنفيذ كامل لنظام "محل شاي" بسيط: `Products` مسطّحة بدون فئات ديناميكية أو كومبوهات، `Sales`/`SaleItems`، `Shifts` بسيطة، ووصفة اختيارية عبر `ProductRecipe`. هذا المستند **لا يعدّل** تلك الكيانات — يضيف طبقة موازية جديدة (`Categories`, `Combos`, `Orders`, `Tables`, `CashShifts`...) تلائم نمط مطعم الوجبات السريعة. القرار بشأن مصير الكيانات القديمة (استبدال كامل أم تشغيل موازٍ ثم ترحيل) موضّح كسؤال مفتوح بالقسم 8 — **يجب حسمه قبل بدء Sprint 2 أدناه**.

**يُعاد استخدامه حرفيًا بدون أي تعديل:**
- `Users`, `Roles`, `Permissions`, `RolePermissions`, `UserPermissionOverride` وكل منطق RBAC/JWT/Global Query Filter الحالي.
- `Branches` (تُضاف له علاقات جديدة فقط، لا تعديل على أعمدته).
- نظام التصميم (Tailwind tokens، Layout.tsx بنسختيه: الكاشير التشغيلي ولوحة الأدمن)، `react-i18next`، الخطوط.
- آلية الطباعة ESC/POS-over-TCP الأساسية (تُوسَّع بالقسم 3.7 وبالـ Brief القسم 3.5، لا تُبنى من الصفر).

---

## 1. نموذج البيانات الأساسي (يقابل "القسم 2" المُشار إليه بالـ Brief)

### 1.1 الفروع والقاعات والطاولات

```
Tables
  Id                UUID PK
  BranchId          UUID FK -> Branches
  Label             TEXT           -- 'Table 1', 'A3'...
  Capacity          INT NULL
  IsActive          BOOLEAN DEFAULT TRUE
  -- ملاحظة: عمود QrCodeToken المذكور بنسخ سابقة من هذا الجدول مُهمَل نهائيًا
  -- لصالح OrderingPoints.QrCodeToken الموحّد (Brief القسم 3.10) — لا يُضاف هنا.

BranchFeatureFlags
  Id                UUID PK
  BranchId          UUID FK -> Branches
  FeatureKey        TEXT   -- 'DineIn' | 'Takeaway' | 'CarPickup' | 'Delivery' | ...
  IsEnabled         BOOLEAN DEFAULT TRUE
  UNIQUE (BranchId, FeatureKey)
```
> مبدأ حاكم (Brief القسم 1.5): أي قسم/ميزة تشغيلية على مستوى الفرع تمر من هذا الجدول، لا أعلام Boolean مباشرة على `Branches`.

### 1.2 القوائم الديناميكية: الفئات والمنتجات والكومبوهات

```
Categories
  Id                UUID PK
  NameAr            TEXT
  NameEn            TEXT
  SortOrder         INT
  IsActive          BOOLEAN DEFAULT TRUE
  -- لا عمود "Type" أو "IsOffer" أو "IsKidsMeal" — العروض ووجبات الأطفال صفوف عادية هنا (Brief القسم 2)

-- CategoryBranchAvailability مُعرَّف بالـ Brief القسم 3.1 — يُستخدم من أول Sprint 2، لا يُعاد تعريفه هنا.

MenuItems                          -- الأساس المشترك بين "منتج مفرد" و"كومبو"
  Id                UUID PK
  CategoryId        UUID FK -> Categories
  NameAr            TEXT
  NameEn            TEXT
  Kind              TEXT           -- 'SingleProduct' | 'Combo'
  BasePrice         NUMERIC(12,3)  -- يُتجاهل لصالح مجموع مكوّنات الكومبو إذا Kind='Combo' وله تسعير محسوب (انظر 1.2.1)
  ImageUrl          TEXT NULL
  SortOrder         INT
  IsActive          BOOLEAN DEFAULT TRUE
  PrinterSectionId  UUID NULL FK -> PrinterSections   -- Brief القسم 3.5، مرتبط هنا مباشرة بدل جدول Products قديم

ComboComponents                    -- فقط عندما MenuItems.Kind = 'Combo'
  Id                UUID PK
  ComboMenuItemId   UUID FK -> MenuItems
  SlotLabel         TEXT           -- 'الصنف الرئيسي', 'الجانبي', 'المشروب'
  IsRequired        BOOLEAN DEFAULT TRUE
  MinSelect         INT DEFAULT 1
  MaxSelect         INT DEFAULT 1

ComboComponentOptions              -- الخيارات المتاحة داخل كل Slot
  Id                UUID PK
  ComboComponentId  UUID FK -> ComboComponents
  MenuItemId        UUID FK -> MenuItems   -- يشير لمنتج مفرد (Kind='SingleProduct') يمكن اختياره بهذا الـ Slot
  PriceDelta        NUMERIC(12,3) DEFAULT 0  -- فرق سعر عند اختيار هذا الخيار بدل الافتراضي (مثال: تكبير الوجبة)
  IsDefault         BOOLEAN DEFAULT FALSE
```

#### 1.2.1 تسعير الكومبو
سعر الكومبو الأساسي = `MenuItems.BasePrice` الخاص بصف الكومبو نفسه (وليس مجموع مكوّناته)، ويُضاف له `PriceDelta` لأي خيار غير افتراضي اختاره الزبون بأي Slot (مثال: تكبير البطاطس). هذا يطابق نمط KFC/McDonald's الفعلي (سعر ثابت للوجبة + فروقات الترقية) بدل جمع أسعار الأصناف المنفردة.

### 1.3 الإضافات (Modifiers)

```
ModifierGroups
  Id                UUID PK
  NameAr            TEXT
  NameEn            TEXT
  MinSelect         INT DEFAULT 0
  MaxSelect         INT DEFAULT 1        -- 1 = اختيار واحد (مثل درجة الحرارة)، أكبر = متعدد (إضافات حرة)
  IsRequired        BOOLEAN DEFAULT FALSE

ModifierOptions
  Id                UUID PK
  ModifierGroupId   UUID FK -> ModifierGroups
  NameAr            TEXT
  NameEn            TEXT
  PriceDelta        NUMERIC(12,3) DEFAULT 0
  IsActive          BOOLEAN DEFAULT TRUE

MenuItemModifierGroups             -- أي مجموعات إضافات تنطبق على أي MenuItem (منتج مفرد فقط)
  MenuItemId        UUID FK -> MenuItems
  ModifierGroupId   UUID FK -> ModifierGroups
  PRIMARY KEY (MenuItemId, ModifierGroupId)
```
> الإضافات (مثل "بدون بصل"، "جبنة إضافية") تُبنى كمجموعات قابلة لإعادة الاستخدام عبر عدة منتجات، لا حقل حر لكل منتج — نفس مبدأ الديناميكية المطبَّق على الفئات.

### 1.4 الطلبات (Orders) — بديل `Sales` لدومين المطعم

```
OrderTypes
  Id                UUID PK
  Code              TEXT UNIQUE   -- 'DINE_IN' | 'TAKEAWAY' | 'CAR_PICKUP' | 'DELIVERY'
  NameAr            TEXT
  NameEn            TEXT

Orders
  Id                UUID PK
  BranchId          UUID FK -> Branches
  OrderNumber       INT            -- تسلسلي لكل فرع، نفس منطق SaleNumber الحالي
  OrderTypeId       UUID FK -> OrderTypes
  TableId           UUID NULL FK -> Tables         -- إلزامي إذا OrderTypeId = DINE_IN
  CarPlateNumber    TEXT NULL                       -- إلزامي إذا OrderTypeId = CAR_PICKUP (Brief القسم 1.4)
  CashierUserId     UUID
  CashShiftId       UUID NULL FK -> CashShifts      -- Brief القسم 3.8، NULL لطلبات لا تمر بصندوق نقدي مباشر
  BusinessDate      DATE
  CreatedAt         TIMESTAMP
  Subtotal          NUMERIC(12,3)
  DiscountAmount    NUMERIC(12,3) DEFAULT 0
  GrandTotal        NUMERIC(12,3)
  Status            TEXT           -- 'Open' | 'Sent' | 'Paid' | 'Closed' | 'Cancelled'
  -- تُضاف لاحقًا: SalesChannelId (Brief 3.9), OrderingSessionId (Brief 3.11) — أعمدة NULL-able من يوم Migration الأولى
  -- لتفادي Migration ثانية عند تنفيذ Sprint 9.9/11، لكن دون تفعيل منطقها قبل ذلك Sprint

OrderItems
  Id                UUID PK
  OrderId           UUID FK -> Orders
  MenuItemId        UUID FK -> MenuItems
  MenuItemNameSnapshot TEXT        -- Snapshot كما بـ SaleItems الحالي
  UnitPriceSnapshot NUMERIC(12,3)  -- شامل فروقات الكومبو المختارة
  Quantity          INT
  LineTotal         NUMERIC(12,3)
  Notes             TEXT NULL      -- ملاحظة حرة من الكاشير ("بدون ثلج")

OrderItemComboSelections           -- فقط إذا OrderItems.MenuItemId يشير لكومبو
  Id                UUID PK
  OrderItemId       UUID FK -> OrderItems
  ComboComponentId  UUID FK -> ComboComponents
  SelectedMenuItemId UUID FK -> MenuItems
  PriceDeltaSnapshot NUMERIC(12,3)

OrderItemModifiers
  Id                UUID PK
  OrderItemId       UUID FK -> OrderItems
  ModifierOptionId  UUID FK -> ModifierOptions
  PriceDeltaSnapshot NUMERIC(12,3)

OrderCancellations                 -- إلغاء صنف/طلب قبل الدفع أو بصلاحية خاصة (منفصل عن OrderEditLogs بعد الإغلاق)
  Id                UUID PK
  OrderId           UUID FK -> Orders
  OrderItemId       UUID NULL FK -> OrderItems     -- NULL = إلغاء الطلب كامل
  Reason            TEXT
  CancelledByUserId UUID
  CreatedAt         TIMESTAMP
```

### 1.5 المخزون: الوصفات (BOM) — الأساس قبل إضافات Brief القسم 3.2/3.3/3.4

```
Ingredients
  Id                UUID PK
  NameAr            TEXT
  NameEn            TEXT
  UnitOfMeasure     TEXT           -- نص حر بهذا الإصدار الأساسي؛ Brief Sprint 6 يستبدله بـ UnitOfMeasureId

BranchIngredientStock
  BranchId          UUID FK -> Branches
  IngredientId      UUID FK -> Ingredients
  CurrentQuantity   NUMERIC(18,3)
  LowStockThreshold NUMERIC(18,3)

MenuItemRecipeLines                -- وصفة اختيارية، بنفس مبدأ ProductRecipe الحالي (منتج بلا سطر = بلا خصم مخزون)
  MenuItemId        UUID FK -> MenuItems
  BranchId          UUID FK -> Branches
  IngredientId      UUID FK -> Ingredients
  QuantityRequired  NUMERIC(18,3)

InventoryTransactions              -- سجل موحّد لكل حركة (توريد، خصم بيع، تسوية جرد...)
  Id                UUID PK
  BranchId          UUID FK -> Branches
  IngredientId      UUID FK -> Ingredients
  QuantityChange    NUMERIC(18,3)  -- سالب = خصم، موجب = إضافة
  TransactionType   TEXT           -- نص حر بهذا الإصدار؛ Brief Sprint 6 يستبدله بـ ReasonId -> InventoryTransactionReasons
  ReferenceOrderId  UUID NULL FK -> Orders    -- مُعبَّأ فقط إذا TransactionType = 'SaleDeduction'
  CreatedByUserId   UUID
  CreatedAt         TIMESTAMP
```

### 1.6 الطباعة (الأساس قبل إضافة `PrinterSections` بالـ Brief 3.5)

```
PrinterConfigs
  Id                UUID PK
  BranchId          UUID FK -> Branches
  Label             TEXT           -- 'كاشير رئيسي', 'مطبخ ساخن'
  IpAddress         TEXT
  Port              INT DEFAULT 9100
  IsActive          BOOLEAN DEFAULT TRUE
```
> `PrinterSections` (الـ Brief القسم 3.5) يُضاف لاحقًا Sprint 9 كجدول منفصل يربط `MenuItems.PrinterSectionId` بـ `PrinterConfigId` — مُعرَّف مسبقًا هنا بعمود `PrinterSectionId` على `MenuItems` (القسم 1.2) لتفادي Migration ثانية.

---

## 2. الدوال الجوهرية (تقابل "الدوال 3.1 إلى 3.7" المُشار إليها بالـ Brief)

### 2.1 بناء القائمة لفرع معيّن
```
FUNCTION GetMenuForBranch(branchId):
    categories = GetAvailableCategoriesForBranch(branchId)   -- Brief القسم 4.1
    FOR category IN categories:
        category.Items = SELECT MenuItems WHERE CategoryId=category.Id AND IsActive=TRUE
                          ORDER BY SortOrder
        FOR item IN category.Items WHERE item.Kind='Combo':
            item.Components = LoadComboComponentsWithOptions(item.Id)
        FOR item IN category.Items WHERE item.Kind='SingleProduct':
            item.ModifierGroups = LoadModifierGroupsFor(item.Id)
    RETURN categories
```

### 2.2 حساب إجمالي الطلب
```
FUNCTION CalculateOrderTotals(orderId):
    order = LoadOrderWithItems(orderId)
    subtotal = 0
    FOR item IN order.Items:
        lineBase = item.UnitPriceSnapshot * item.Quantity
        modifiersTotal = SUM(OrderItemModifiers.PriceDeltaSnapshot WHERE OrderItemId=item.Id) * item.Quantity
        item.LineTotal = lineBase + modifiersTotal
        subtotal += item.LineTotal
    order.Subtotal = subtotal
    order.GrandTotal = subtotal - order.DiscountAmount
    SAVE order
```

### 2.3 التحقق من توفر المخزون وخصمه عند إرسال الطلب (Atomic)
```
FUNCTION ConfirmOrderAndDeductStock(orderId):
    order = LoadOrderWithItems(orderId)
    BEGIN TRANSACTION
        FOR item IN order.Items:
            recipeLines = MenuItemRecipeLines WHERE MenuItemId=item.MenuItemId AND BranchId=order.BranchId
            IF item.MenuItemId is Combo: recipeLines += recipe lines لكل OrderItemComboSelections المرتبطة
            FOR line IN recipeLines:
                required = line.QuantityRequired * item.Quantity
                stock = BranchIngredientStock WHERE BranchId=order.BranchId AND IngredientId=line.IngredientId
                IF stock.CurrentQuantity < required:
                    ROLLBACK; THROW "مخزون غير كافٍ: " + line.Ingredient.NameAr
        FOR item IN order.Items:
            -- نفس حلقة الخصم الفعلي بعد التأكد من كفاية الكل (لتفادي خصم جزئي)
            DEDUCT stock accordingly
            INSERT InventoryTransactions (..., TransactionType='SaleDeduction', ReferenceOrderId=orderId)
        UPDATE order SET Status='Sent'
    COMMIT
```
> نفس نمط الذرّية (Atomic Transaction) المستخدم أصلاً بمنطق خصم مخزون `Sales` الحالي — لا آلية جديدة، فقط مُطبَّقة على `Orders`/`MenuItemRecipeLines`.

### 2.4 إلغاء صنف/طلب قبل الدفع
```
FUNCTION CancelOrderItem(orderId, orderItemId, reason, userId):
    IF NOT UserHasPermission(userId, 'orders.cancel'):
        THROW "صلاحية غير كافية"
    item = OrderItems WHERE Id=orderItemId AND OrderId=orderId
    IF order.Status IN ('Paid','Closed'): THROW "لا يمكن إلغاء صنف من طلب مغلق — استخدم EditClosedOrder"
    IF item stock already deducted (order.Status='Sent'):
        REVERSE matching InventoryTransactions (إرجاع الكمية)
    DELETE item (أو وسمه Cancelled حسب قرار التنفيذ)
    INSERT OrderCancellations (OrderId, OrderItemId, Reason, CancelledByUserId)
    CalculateOrderTotals(orderId)
```

### 2.5 طباعة فاتورة العميل (الأساس — يُدمج مع `PrintOrderTickets` بالـ Brief 4.2 ضمن Sprint 9)
```
FUNCTION PrintReceipt(orderId):
    order = LoadOrderWithItems(orderId)
    receiptHtml = RenderReceiptTemplate(order)   -- نفس مكوّن Receipt.tsx الحالي بالمبدأ، مُكيَّف لحقول Orders
    image = HeadlessBrowserScreenshot(receiptHtml)
    escPosBytes = ConvertImageToEscPosRaster(image)
    printerConfig = GetDefaultPrinterConfig(order.BranchId)
    SendTcpRaw(printerConfig.IpAddress, printerConfig.Port, escPosBytes)
```

---

## 3. خطة السبرنتات الأساسية 0–12 (يبني عليها الـ Brief تعديلاته بالقسم 6)

> السبرنتات المُعلَّمة **(الـ Brief يعدّلها)** موصوفة بالتفصيل الكامل بملف `OFC-Development-Brief.md` القسم 6 — هنا فقط نطاقها الأساسي (Baseline) الذي تُطبَّق عليه تلك التعديلات.

### Sprint 0 — الإعداد والبنية التحتية
مطابق حرفيًا لـ Sprint 0 بملف `pos-system-sprint-prompt.md` الحالي (Solution structure، React+Vite، Supabase connection، i18n، Swagger) — **لا حاجة لإعادة تنفيذه**، البنية موجودة فعليًا بالمستودع ويُعاد استخدامها.

### Sprint 1 — الهوية والصلاحيات
**مُعاد استخدام بالكامل** من النظام الحالي (`Users`/`Roles`/`Permissions`/`RolePermissions`/`UserPermissionOverride`، JWT، Global Query Filter). الإضافة الوحيدة: صلاحيات جديدة بجدول `Permissions` الموجود:
`orders.create`, `orders.cancel`, `combos.manage`, `modifiers.manage`, `tables.manage`, `printing.manage`, `channels.manage` (إن لم تكن موجودة), `closedOrders.edit` (يقابل `CanEditClosedOrder` بالـ Brief), `debtPayments.approve` (يقابل `CanApproveDebtPayment`), `orders.transfer` (يقابل `CanTransferOrder`).
**Acceptance:** الصلاحيات الجديدة تُمنح/تُسحب بنفس شاشة إدارة الصلاحيات الفردية الموجودة، بدون أي تعديل على منطق RBAC نفسه.

### Sprint 2 — الفروع والطاولات والفئات والمنتجات والكومبوهات

> **حالة التنفيذ:** الكيانات (`Table`, `BranchFeatureFlag`, `Category`, `CategoryBranchAvailability`, `MenuItem`, `ComboComponent`, `ComboComponentOption`) وخدمات الـ Application layer والـ Controllers مكتوبة على فرع `ofc-fastfood-sprint1` (غير مدموج بـ `main` بعد). **لم تُولَّد Migration الـ EF Core بعد** — البيئة التي كتبت فيها هذا الكود لا تملك .NET SDK ولا اتصال بقاعدة Supabase، فتوليد/تعديل `AppDbContextModelSnapshot.cs` يدويًا خطر غير مقبول (يؤثر على كل Migration مستقبلية). **قبل تشغيل هذا الفرع، لازم تُنفَّذ يدويًا خطوة واحدة من بيئة فيها dotnet SDK:**
> ```
> cd backend/src/POS.API
> dotnet ef migrations add OfcSprint2AddTablesCategoriesMenuItemsCombos --project ../POS.Infrastructure --startup-project .
> ```
> ثم مراجعة الـ Migration المُولَّدة والتأكد من نجاح `dotnet build` قبل أي دمج بـ `main`. لوحات الإدارة بالفرونت إند (شاشات الفئات/المنتجات/الطاولات) لم تُبنَ بعد ضمن هذا الـ Sprint — الأولوية كانت الأساس الخلفي (Backend) أولًا.
Migrations لكل كيانات القسم 1.1 و1.2 أعلاه (`Tables`, `BranchFeatureFlags`, `Categories`, `MenuItems`, `ComboComponents`, `ComboComponentOptions`) + `CategoryBranchAvailability` (Brief 3.1). شاشات إدارية: الفئات (سحب وإفلات للترتيب)، المنتجات المفردة، بناء الكومبو (اختيار Slots وخياراتها). **Acceptance:** حسب الـ Brief القسم 6 (فئة "Offers" تُبنى بدون كود إضافي) + إنشاء كومبو "وجبة برجر" بثلاث Slots (رئيسي/جانبي/مشروب) وكل Slot له خيارات بفروقات سعر مختلفة.

### Sprint 3 — الإضافات (Modifiers)
Migrations لـ `ModifierGroups`/`ModifierOptions`/`MenuItemModifierGroups`. شاشة إدارية لبناء مجموعات الإضافات وربطها بمنتجات متعددة. بواجهة الكاشير: عند إضافة صنف له مجموعات إضافات، تفتح نافذة اختيار سريعة قبل إضافته للسلة. **Acceptance:** منتج "برجر دجاج" مرتبط بمجموعة "درجة الحار" (اختيار واحد إلزامي) ومجموعة "إضافات" (متعدد اختياري) — إضافته للسلة بدون اختيار "درجة الحار" تُرفض، بدون اختيار "إضافات" تُقبل بلا فرق سعر.

### Sprint 4 — إدارة الطلبات الأساسية (Dine-in / Takeaway)
Migrations لـ `OrderTypes` (Seed أولي: DINE_IN, TAKEAWAY, CAR_PICKUP, DELIVERY) و`Orders`/`OrderItems`/`OrderItemComboSelections`/`OrderItemModifiers`. تطبيق الدالتين 2.1 و2.2 أعلاه. واجهة كاشير مبنية على نفس مكوّنات شاشة الكاشير الحالية (`Layout.tsx` التشغيلي، شبكة منتجات، سلة) لكن مربوطة بـ `Orders` بدل `Sales`، مع دعم اختيار `OrderTypeId` وربط `TableId` عند Dine-in. **Acceptance:** طلب Dine-in على طاولة معيّنة بصنف كومبو (بخيارات مخصصة) وصنف مفرد بإضافات — الإجمالي يُحسب صحيحًا شاملًا كل الفروقات، ويظهر رقم الطاولة بالفاتورة.

### Sprint 5 — الإلغاء وسجل التدقيق
Migration لـ `OrderCancellations` + الدالة 2.4. واجهة: زر إلغاء لكل صنف بالسلة (قبل الدفع)، وشاشة "سجل الإلغاءات" لكل فرع/كاشير/فترة. **Acceptance:** إلغاء صنف من طلب مُرسَل للمخزون (Status='Sent') يُرجع الكمية المخصومة تلقائيًا لـ `BranchIngredientStock` ويُسجَّل السبب والمستخدم.

### Sprint 6 — المخزون: الوصفات (BOM) والاستهلاك النظري
**يُنفَّذ مباشرة بنسخته المُعدَّلة من الـ Brief القسم 6** (مع `Warehouses`/`UnitsOfMeasure`/`InventoryTransactionReasons` من اليوم الأول) — لا داعي لتنفيذ نسخة أساسية ثم تعديلها لاحقًا، فقط Migrations لكيانات القسم 1.5 أعلاه معدَّلة بإضافات الـ Brief مباشرة قبل تفعيل الدالة 2.3.

### Sprint 7 — الجرد الفعلي ومقارنة الفروقات
(هذا البند مذكور بجدول الأولويات بالـ Brief القسم 7 كبند #8 منفصل عن BOM، ولم يُفصَّل بالـ Brief لأنه يتبع نمط "الجرد" العام بالملف الأصلي المفقود — يُعرَّف هنا):
```
StockCounts
  Id                UUID PK
  BranchId          UUID FK -> Branches
  WarehouseId       UUID FK -> Warehouses
  CountedByUserId   UUID
  CreatedAt         TIMESTAMP
  Status            TEXT   -- 'Draft' | 'Finalized'

StockCountLines
  StockCountId      UUID FK -> StockCounts
  IngredientId      UUID FK -> Ingredients
  SystemQuantity    NUMERIC(18,3)   -- من BranchIngredientStock وقت بدء الجرد
  CountedQuantity   NUMERIC(18,3)
  VarianceQuantity  NUMERIC(18,3)   -- CountedQuantity - SystemQuantity
```
عند `Finalized`: يُنشأ `InventoryTransactions` بفارق كل مكوّن (سبب `INVENTORY_COUNT_ADJUSTMENT`)، ويُحدَّث `BranchIngredientStock` ليطابق الفعلي. **Acceptance:** جرد فرع يكتشف نقص 2 كغم دقيق — عند الاعتماد يُسجَّل تعديل مخزون بسبب واضح ويتغيّر الرصيد الحالي فورًا.

### Sprint 8 — Car Pickup + Feature Flags
**كما هو مؤكَّد حرفيًا بالـ Brief القسم 6** — `CarPlateNumber` على `Orders` (موجود مسبقًا بالقسم 1.4 أعلاه) + تفعيل `BranchFeatureFlags.CarPickup` من لوحة التحكم. بدون أي مراحل Drive-Thru إضافية.

### Sprint 9 — الطباعة المتكاملة
**كما هو مفصَّل بالـ Brief القسم 6** (`PrinterSections` + دمج `PrintReceipt` مع `PrintOrderTickets`).

### Sprint 9.5, 9.75, 9.9 — الدفع/تعديل الفواتير، إغلاق الصندوق، قنوات البيع
**مفصَّلة بالكامل بالـ Brief القسم 6** — لا إضافة هنا.

### Sprint 10 — التقارير ولوحات المتابعة (الأساس)
مبني على نفس بنية `ReportsPage.tsx`/`ReportService` الحالية، لكن مصدر البيانات `Orders`/`OrderItems` بدل `Sales`/`SaleItems`: مبيعات لكل فرع/فترة، الأصناف الأكثر مبيعًا (تشمل الكومبوهات كوحدة، لا تُفكَّك لمكوّناتها بالتقرير الافتراضي)، توزيع أنواع الطلبات (Dine-in/Takeaway/Car Pickup/Delivery). الـ Brief القسم 6 يضيف فوقه لاحقًا تقرير فروقات الصناديق وتعديلات الفواتير.

### Sprint 11 — QR Ordering
**مفصَّل بالكامل بالـ Brief القسم 6** (`OrderingPoints`, `OrderingSessions`, `CarPickupBays`).

### Sprint 12 — UAT والنشر
تجربة قبول من فرع حقيقي واحد + خطة توسّع للأفرع الباقية، بنفس منهجية Sprint 8 بملف `pos-system-sprint-prompt.md` الحالي.

---

## 4. قرارات معلّقة تخص هذا الملف تحديدًا (بالإضافة لقسم 9 بالـ Brief)

1. ~~الأهم — استراتيجية الترحيل~~ **[محسوم]** استبدال كامل: `Orders`/`OrderItems`/`CashShifts` تحل محل `Sales`/`SaleItems`/`Shifts` نهائيًا. ترتيب التنفيذ: تُبنى الكيانات والمنطق الجديد أولًا (Sprints 1–6) بدون حذف القديم، ثم يُرحَّل الكاشير التشغيلي والتقارير للاعتماد على `Orders` (نهاية Sprint 4 فصاعدًا)، ثم يُحذف `Sales`/`SaleItems`/`Shifts`/`ShiftCashCount`/`SaleInventoryConsumption`/`SaleEdit`/`VoidRequest` وكل مرجع لها بعد التأكد من عدم وجود اعتماد باقٍ عليها (Sprint قريب من 10-12، وليس قبل ذلك — البيانات التاريخية بهذه الجداول تبقى للتقارير القديمة حتى قرار أرشفة منفصل).
2. تسعير الكومبو (القسم 1.2.1) بنمط "سعر ثابت + فروقات ترقية" افتراض مني بناءً على نمط KFC/McDonald's المذكور بالـ Brief — يحتاج تأكيدك أو تعديل إذا كان المقصود شيء مختلف (مثلاً خصم نسبي عن مجموع الأصناف المنفردة).
3. عند إلغاء صنف كومبو (Sprint 5) — هل الإرجاع للمخزون يشمل كل مكوّنات الـ Slots المختارة تلقائيًا؟ افتراضي بالدالة 2.4 نعم، يحتاج تأكيد.
4. Sprint 7 (الجرد الفعلي) من تصميمي أنا بالكامل لأن الـ Brief لم يفصّله — يحتاج مراجعتك تحديدًا أكثر من غيره.
