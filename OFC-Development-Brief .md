# OFC System — Development Brief (للمُنفِّذ / Development AI Agent)

> **دور هذا المستند:** هذا Brief تخطيطي نهائي (Planning) مُعدّ من طرف مسؤول التخطيط المعماري للمشروع. أنت (المساعد المُنفِّذ) مسؤول عن **التطبيق الفعلي فقط** — لا تُعيد التخطيط، لا تُعيد تصميم الـ Schema من الصفر، ولا تقترح بدائل معمارية إلا إذا وجدت تعارضًا فنيًا حقيقيًا يستحيل تطبيقه، وفي هذه الحالة توقّف واسأل بدل الاجتهاد.

---

## 0. السياق والمصادر

| العنصر | القيمة |
|---|---|
| المشروع المصدر (Architecture Clone) | `https://github.com/Almutasim90/lolat-suwaiq` (نظام محل شاي وحلويات — القاعدة المعمارية) |
| المستودع الجديد المستهدف | `https://github.com/Almutasim90/ofc` |
| قاعدة البيانات | Supabase instance **جديد ومنفصل تمامًا** (بدون مشاركة بيانات مع lolat-suwaiq) |
| نمط الأعمال | مطعم وجبات سريعة (Fast Food)، نموذج تشغيلي يشبه KFC/McDonald's من ناحية سير العمل (Combos/Meals, Upsize, Car Pickup) — **وليس مطلوبًا تقليد واجهاتهم أو نسخ سلوكهم حرفيًا**، فقط الاستفادة من نفس منطق التشغيل العام. |
| مصدر الـ Benchmarking الوظيفي | لقطات شاشة من نظام تشغيلي حالي (Billbox POS by Malabarsoft) — **استُخدمت فقط لاستخراج مفاهيم وظيفية ناقصة من المخطط، وليست مرجعًا للتصميم أو الواجهة بأي شكل.** التصميم النهائي للواجهات هو التصميم الاحترافي المعتمد سابقًا والمُجرَّب فعليًا في lolat-suwaiq — **يُعاد استخدامه كما هو، لا يُعاد تصميمه.** |

---

## 1. قرارات معمارية نهائية (Non-negotiable)

1. **Clean Architecture / Modular Monolith** كما في lolat-suwaiq — ASP.NET Core Web API + React (Tajawal font, RTL/LTR ثنائي اللغة).
2. **Supabase = DB مُدارة فقط** — بدون Supabase Auth. الـ Auth/Roles/Permissions مُعاد استخدامها من lolat-suwaiq كما هي.
3. **الطباعة: ESC/POS raw عبر TCP Socket فقط (Port 9100 افتراضيًا)** — **تم حسم النقاش: لا يوجد KDS (Kitchen Display System) في هذه المرحلة ولا في أي Sprint قادم ما لم يُطلب صراحة لاحقًا كمشروع منفصل.** أي طلب/تعديل يخص "شاشة مطبخ" يُرفض أو يُؤجَّل تلقائيًا إلى Backlog منفصل، ولا يُدمج ضمن السبرنتات الحالية.
4. **Car Pickup = حقل رقم لوحة فقط (كما هو مخطط له أصلًا)** — **تم حسم النقاش: لا Drive-Thru بمراحل متعددة (Order/Pay/Pickup) في هذه المرحلة.** لا تُضِف جداول أو حالات إضافية لهذا الغرض.
5. **كل شيء Configuration-driven / Dynamic على مستوى الفرع** — هذا مبدأ حاكم فوق كل الجداول التالية، وليس فقط `BranchFeatureFlags`. أي "قسم/فئة/عرض" بالقائمة يجب أن يُضاف/يُعدَّل/يُعطَّل من لوحة التحكم دون أي تعديل كود أو Deployment جديد.
6. **لا Multi-tenancy مشترك** — مشروع منفصل بالكامل عن lolat-suwaiq، لا `tenant_id` عابر للمشروعين.

---

## 2. مبدأ "الديناميكية" — التوضيح الحاسم قبل البدء

الطلب الصريح: **العروض (Offers)** و**Kids Meal** ليست كيانات خاصة (Special Entities) بجداول منفصلة، بل هي **فئات (Categories) عادية تمامًا** بنفس آلية أي قسم آخر بالقائمة (تمامًا كما يحدث في نظام الشاي `lolat-suwaiq` حاليًا). أي:

- ✅ **لا** جدول `Offers` منفصل، **لا** جدول `KidsMeals` منفصل.
- ✅ "العروض" = صف جديد في `Categories` (مثلاً `NameEn = 'Offers'`)، وبداخله `Products`/`Combos` عادية تمامًا مرتبطة بهذا التصنيف.
- ✅ "Kids Meal" = صف آخر في `Categories`، وبداخله Combos (وجبة أطفال = Combo فيه برجر صغير + بطاطس صغيرة + مشروب صغير + لعبة كمنتج مستقل ضمن الكومبو مثلًا).
- ✅ الإداري يقدر: يضيف فئة جديدة، يعطّلها مؤقتًا (بدون حذف)، يعدّل ترتيبها، يعدّل محتواها (منتجات/كومبوهات) — **كل هذا من واجهة الإدارة بدون أي تدخل مطور**.
- ✅ يجب أن تكون الفئة قابلة للتفعيل/التعطيل **على مستوى الفرع** أيضًا (مو بس على مستوى النظام كامل) — مثال: عرض موسمي مفعّل بفرع السويق فقط.

> **مبدأ عام يُطبَّق على كل الميزات المستقبلية:** أي "ميزة" أو "قسم" جديد بالقائمة (عروض، وجبات أطفال، أو أي فئة مستقبلية غير معروفة الآن) يجب أن يمر عبر نفس المسار العام (`Categories` + `Products`/`Combos` + توفر على مستوى الفرع)، **وليس بإضافة كود/جدول خاص لكل ميزة جديدة.** هذا هو معيار قبول (Acceptance Criteria) لأي Sprint يلمس القائمة.

---

## 3. تحديثات على نموذج قاعدة البيانات (فوق ما ورد في `OFC-System-Detailed-Spec.md`)

> الجداول التالية **إضافة** على المخطط الأصلي (القسم 2 بالملف المرجعي)، ولا تُلغي أو تُعدّل أي جدول موجود إلا حيث يُذكر صراحة.

### 3.1 توفر الفئة على مستوى الفرع (لدعم الديناميكية المطلوبة في القسم 2 أعلاه)

```sql
CategoryBranchAvailability
  Id                UUID PK
  CategoryId        UUID FK -> Categories
  BranchId          UUID FK -> Branches
  IsAvailable       BOOLEAN DEFAULT TRUE
  UNIQUE (CategoryId, BranchId)
```
> إن لم يوجد صف لفرع معيّن يُعتبر متاحًا افتراضيًا (Fail-open) لتفادي كسر الفروع الجديدة التي لم تُهيَّأ بعد. يمكن مراجعة هذا الافتراض لاحقًا مع الفريق.

### 3.2 وحدات القياس كجدول مرجعي (بدل نص حر في `Ingredients.UnitOfMeasure`)

```sql
UnitsOfMeasure
  Id                UUID PK
  Name              TEXT UNIQUE   -- 'NOS', 'Kilogram', 'Litre', 'Gram', ...
  Symbol            TEXT
  IsBase            BOOLEAN DEFAULT FALSE

-- تعديل على Ingredients:
Ingredients
  ...
  UnitOfMeasureId   UUID FK -> UnitsOfMeasure   -- بدل الحقل النصي UnitOfMeasure
```

### 3.3 مخازن متعددة لكل فرع

```sql
Warehouses
  Id                UUID PK
  BranchId          UUID FK -> Branches
  NameAr            TEXT
  NameEn            TEXT
  IsDefault         BOOLEAN DEFAULT TRUE
  IsActive          BOOLEAN DEFAULT TRUE

-- تعديل: BranchIngredientStock و InventoryTransactions تصبح مرتبطة بـ WarehouseId
-- بدل BranchId مباشرة (WarehouseId -> Warehouses -> BranchId يحل المرجعية)
```

### 3.4 أسباب حركة المخزون (بدل TransactionType كنص حر فقط)

```sql
InventoryTransactionReasons
  Id                UUID PK
  Code              TEXT UNIQUE   -- 'PURCHASE_IN' | 'TRANSFER_IN' | 'TRANSFER_OUT' | 'WASTE' | 'THEORETICAL_CONSUMPTION' | 'MANUAL_ADJUST'
  NameAr            TEXT
  NameEn            TEXT
  IsActive          BOOLEAN DEFAULT TRUE

-- تعديل: InventoryTransactions.TransactionType يصبح ReasonId UUID FK -> InventoryTransactionReasons
```

### 3.5 توجيه الطباعة على مستوى المنتج/القسم (وليس KDS)

```sql
PrinterSections                      -- مثال: 'مطبخ ساخن', 'مشروبات', 'كاشير'
  Id                UUID PK
  BranchId          UUID FK -> Branches
  NameAr            TEXT
  NameEn            TEXT
  PrinterConfigId   UUID FK -> PrinterConfigs

-- تعديل على Products:
Products
  ...
  PrinterSectionId  UUID NULL FK -> PrinterSections   -- أي طابعة يُطبع عليها هذا الصنف
```
> هذا **لا علاقة له بـ KDS** — هو فقط توجيه ورقة الطباعة الفعلية (Ticket) إلى طابعة فيزيائية مختلفة حسب القسم (مثلاً المشروبات تُطبع بطابعة البار، الأصناف الساخنة بطابعة المطبخ)، ويبقى ضمن نفس آلية ESC/POS-TCP الموضحة بالقسم 3.7 من الملف الأصلي.

### 3.6 طرق الدفع (Payments) — بما فيها "الدَين" كاستثناء وليس أساسًا

```sql
PaymentMethods
  Id                UUID PK
  Code              TEXT UNIQUE   -- 'CASH' | 'CARD' | 'DEBT'
  NameAr            TEXT
  NameEn            TEXT
  RequiresApproval  BOOLEAN DEFAULT FALSE   -- TRUE لـ 'DEBT' مثلًا
  IsActive          BOOLEAN DEFAULT TRUE

OrderPayments
  Id                UUID PK
  OrderId           UUID FK -> Orders
  PaymentMethodId   UUID FK -> PaymentMethods
  Amount            NUMERIC(12,3)
  ApprovedByUserId  UUID NULL    -- إلزامي إذا PaymentMethod.RequiresApproval = TRUE
  CreatedAt         TIMESTAMP
```
> طلب واحد قد يُدفع بأكثر من طريقة (Split Payment) — لذلك الجدول منفصل عن `Orders` وليس حقلًا واحدًا بداخله.

### 3.7 تعديل الفواتير بعد الإغلاق (Order Edit Audit) — أوسع من `OrderCancellations`

```sql
OrderEditLogs
  Id                UUID PK
  OrderId           UUID FK -> Orders
  UserId            UUID
  EditType          TEXT   -- 'ItemAdded' | 'ItemRemoved' | 'PartialRefund' | 'PriceOverride' | 'Other'
  Notes             TEXT NULL
  AmountDelta       NUMERIC(12,3) DEFAULT 0   -- موجب أو سالب حسب نوع التعديل
  CreatedAt         TIMESTAMP
```
> هذا يوثّق أي تعديل على طلب **بعد** أن يصبح Status = 'Paid'/'Closed'، بشكل منفصل عن `OrderCancellations` (التي تبقى مخصصة للإلغاء الكامل لعنصر/طلب بسبب محدد قبل الدفع أو بصلاحية خاصة).

### 3.8 إغلاق الصندوق النقدي (Cash Reconciliation)

```sql
CashShifts
  Id                UUID PK
  BranchId          UUID FK -> Branches
  OpenedByUserId    UUID
  ClosedByUserId    UUID NULL
  OpeningFloat      NUMERIC(12,3)   -- المبلغ الافتتاحي بالدرج
  OpenedAt          TIMESTAMP
  ClosedAt          TIMESTAMP NULL
  Status            TEXT  -- 'Open' | 'Closed'

CashCounts
  Id                UUID PK
  CashShiftId       UUID FK -> CashShifts
  DenominationValue NUMERIC(12,3)   -- مثال: 50, 20, 10, 5, 1, 0.500, 0.100, 0.050, 0.025
  DenominationType  TEXT   -- 'Note' | 'Coin'
  CountedQty        INT
  CreatedAt         TIMESTAMP

-- عند إغلاق الوردية:
-- ExpectedCash = OpeningFloat + SUM(OrderPayments WHERE PaymentMethod='CASH' للوردية)
-- CountedCash  = SUM(DenominationValue * CountedQty) من CashCounts
-- VarianceCash = CountedCash - ExpectedCash  (يُخزَّن على CashShifts أو يُحسب Live)
```

### 3.9 قنوات البيع (Sales Channels) — مفهوم مستقل عن `OrderTypes`

> **توضيح حاسم:** `OrderTypes` (Dine-in/Takeaway/Car Pickup/Delivery) يجاوب على "طبيعة الطلب". `SalesChannels` يجاوب على سؤال مختلف: "من أين دخل الطلب؟" (كاشير، QR طاولة، QR سيارة، اتصال هاتفي، تطبيق توصيل خارجي...). الاثنان مستقلان ويُسجَّلان معًا على كل طلب.

```sql
SalesChannels
  Id                UUID PK
  Code              TEXT UNIQUE   -- 'IN_STORE' | 'QR_TABLE' | 'QR_CAR' | 'CALL_CENTER' | 'AGGREGATOR_*'
  NameAr            TEXT
  NameEn            TEXT
  IsActive          BOOLEAN DEFAULT TRUE

BranchSalesChannelAvailability
  Id                UUID PK
  SalesChannelId    UUID FK -> SalesChannels
  BranchId          UUID FK -> Branches
  IsAvailable       BOOLEAN DEFAULT TRUE
  RequiresPrepayment BOOLEAN DEFAULT FALSE   -- القرار: هل يلزم الدفع الإلكتروني قبل إرسال الطلب للمطبخ؟ (Feature Flag لكل فرع/قناة كما طُلب)
  UNIQUE (SalesChannelId, BranchId)

-- تعديل على Orders:
Orders
  ...
  SalesChannelId    UUID FK -> SalesChannels
```

### 3.10 نقطة الطلب العامة للـ QR (تعميم بدل ربط QR بالطاولات فقط)

```sql
CarPickupBays
  Id                UUID PK
  BranchId          UUID FK -> Branches
  BayLabel          TEXT    -- 'Bay 1', 'Bay 2'
  IsActive          BOOLEAN DEFAULT TRUE

OrderingPoints
  Id                UUID PK
  BranchId          UUID FK -> Branches
  PointType         TEXT    -- 'TABLE' | 'CAR_BAY'
  LinkedTableId     UUID NULL FK -> Tables       -- يُملأ فقط إذا PointType='TABLE'
  LinkedCarBayId    UUID NULL FK -> CarPickupBays -- يُملأ فقط إذا PointType='CAR_BAY'
  QrCodeToken       TEXT UNIQUE
  IsActive          BOOLEAN DEFAULT TRUE
```
> ملاحظة: `Tables.QrCodeToken` المذكور بالملف الأصلي (القسم 2.1) يُهمَل لصالح `OrderingPoints.QrCodeToken` الموحّد، لتفادي ازدواجية مصدر الحقيقة (Single Source of Truth) بين نوعي نقاط الطلب.

### 3.11 جلسة الطاولة/النقطة (Ordering Session) — لتطبيق أفضل الممارسات بتجميع الطلبات

```sql
OrderingSessions
  Id                UUID PK
  OrderingPointId   UUID FK -> OrderingPoints
  Status            TEXT    -- 'Open' | 'Closed'
  OpenedAt          TIMESTAMP
  ClosedAt          TIMESTAMP NULL

  -- قيد صارم: لا يُسمح بأكثر من صف واحد بـ Status='Open' لنفس OrderingPointId في أي وقت
  -- (Partial Unique Index على OrderingPointId WHERE Status='Open') — لمنع تداخل عائلتين على نفس الطاولة

-- تعديل على Orders:
Orders
  ...
  OrderingSessionId UUID NULL FK -> OrderingSessions   -- NULL لأي طلب غير QR (كاشير عادي مثلًا)
```
> **القاعدة المتبعة (Best Practice):** كل الطلبات المُضافة عبر نفس `OrderingSessionId` المفتوحة تُجمَّع داخل **نفس** سجل `Orders` (نفس الفاتورة)، وليس عدة طلبات منفصلة لنفس الطاولة. القسمة عند الدفع (لكل شخص حصته، أو بالتساوي) تُطبَّق عبر `OrderPayments` (القسم 3.6) دون الحاجة لأي جدول إضافي.

> **قاعدة عزل صارمة (منع تداخل عائلتين على نفس الطاولة):** القيد الفريد أعلاه يمنع فتح جلسة جديدة على نفس `OrderingPointId` طالما فيه جلسة `Open` قائمة. **لا تُفتح جلسة جديدة إلا بعد إغلاق القديمة رسميًا** عبر `CloseOrderingSession` (القسم 4.9)، وعند الإغلاق **يُعاد توليد `QrCodeToken` تلقائيًا** لنفس `OrderingPoint` (القسم 4.10) — بحيث أي شخص احتفظ بصورة الرمز القديم بجواله لا يقدر يطلب بالغلط على طاولة العائلة الجديدة.

---

## 4. تحديثات على الدوال الأساسية (فوق القسم 3 من `OFC-System-Detailed-Spec.md`)

> الدوال 3.1 إلى 3.7 بالملف الأصلي **تبقى كما هي بدون تعديل**. الإضافات فقط:

### 4.1 التحقق من توفر الفئة بالفرع (يُستدعى عند بناء القائمة لأي فرع)

```
FUNCTION GetAvailableCategoriesForBranch(branchId):
    allCategories = SELECT * FROM Categories WHERE IsActive = TRUE ORDER BY SortOrder
    result = []
    FOR category IN allCategories:
        availability = CategoryBranchAvailability WHERE CategoryId=category.Id AND BranchId=branchId
        IF availability IS NULL OR availability.IsAvailable = TRUE:
            result.APPEND(category)
    RETURN result
```

### 4.2 توجيه الطباعة حسب القسم عند تأكيد الطلب

```
FUNCTION PrintOrderTickets(orderId):
    order = LoadOrderWithItems(orderId)
    itemsBySection = GROUP order.Items BY item.Product.PrinterSectionId
    FOR section, items IN itemsBySection:
        printerConfig = section.PrinterConfigId != NULL 
                         ? section.PrinterConfig 
                         : GetDefaultPrinterConfig(order.BranchId)
        ticketHtml = RenderKitchenTicketTemplate(order, items)
        image = HeadlessBrowserScreenshot(ticketHtml)
        escPosBytes = ConvertImageToEscPosRaster(image)
        SendTcpRaw(printerConfig.IpAddress, printerConfig.Port, escPosBytes)
    -- PrintReceipt(orderId) بالقسم 3.7 الأصلي يبقى منفصلاً لفاتورة العميل نفسها
```

### 4.3 تسجيل الدفع (يدعم Split Payment + اعتماد الدَين)

```
FUNCTION RecordOrderPayment(orderId, paymentMethodCode, amount, userId):
    method = PaymentMethods WHERE Code = paymentMethodCode
    IF method.RequiresApproval AND NOT UserHasPermission(userId, 'CanApproveDebtPayment'):
        THROW "صلاحية غير كافية لاعتماد الدفع الآجل"
    INSERT OrderPayments (OrderId=orderId, PaymentMethodId=method.Id, Amount=amount,
                          ApprovedByUserId = method.RequiresApproval ? userId : NULL)
    totalPaid = SUM(OrderPayments.Amount WHERE OrderId=orderId)
    IF totalPaid >= order.GrandTotal:
        UPDATE Orders SET Status = 'Paid'
```

### 4.4 تعديل طلب بعد الإغلاق (Audit)

```
FUNCTION EditClosedOrder(orderId, editType, amountDelta, notes, userId):
    IF NOT UserHasPermission(userId, 'CanEditClosedOrder'):
        THROW "صلاحية غير كافية لتعديل فاتورة مغلقة"
    APPLY the actual change (item add/remove/refund/override) على Orders/OrderItems
    RECALCULATE order totals (CalculateOrderTotals من القسم 3.2 الأصلي)
    INSERT OrderEditLogs (OrderId, UserId, EditType, Notes, AmountDelta)
```

### 4.5 حساب فروقات الصندوق النقدي عند الإغلاق

```
FUNCTION CloseCashShift(cashShiftId, countedDenominations[], userId):
    shift = CashShifts WHERE Id = cashShiftId AND Status = 'Open'
    IF shift IS NULL: THROW "لا توجد وردية مفتوحة"
    FOR denom IN countedDenominations:
        INSERT CashCounts (CashShiftId, DenominationValue, DenominationType, CountedQty)
    expectedCash = shift.OpeningFloat + SUM(OrderPayments.Amount 
                    WHERE PaymentMethodId = 'CASH' AND OrderId IN (orders during shift))
    countedCash = SUM(DenominationValue * CountedQty FROM CashCounts WHERE CashShiftId=cashShiftId)
    varianceCash = countedCash - expectedCash
    UPDATE CashShifts SET Status='Closed', ClosedByUserId=userId, ClosedAt=NOW()
    RETURN { expectedCash, countedCash, varianceCash }
```

### 4.6 حل رمز QR وفتح/الانضمام لجلسة الطاولة

```
FUNCTION ResolveQrScan(qrToken):
    point = OrderingPoints WHERE QrCodeToken = qrToken AND IsActive = TRUE
    IF point IS NULL: THROW "رمز غير صالح أو مُعطَّل"

    session = OrderingSessions WHERE OrderingPointId = point.Id AND Status = 'Open'
    IF session IS NULL:
        session = INSERT OrderingSessions (OrderingPointId=point.Id, Status='Open', OpenedAt=NOW())
    -- أي شخص ثانٍ يمسح نفس الـ QR وبنفس الجلسة المفتوحة ينضم لنفس session تلقائيًا

    RETURN { branchId: point.BranchId, pointType: point.PointType, session }
```

### 4.7 إضافة طلب لجلسة قائمة (تجميع فاتورة الطاولة — Best Practice)

```
FUNCTION AddQrOrderToSession(sessionId, items[], salesChannelCode):
    session = OrderingSessions WHERE Id = sessionId AND Status = 'Open'
    IF session IS NULL: THROW "الجلسة غير مفتوحة"

    order = Orders WHERE OrderingSessionId = sessionId AND Status = 'Open'
    IF order IS NULL:
        order = CreateOrder(OrderingSessionId=sessionId, 
                             SalesChannelId=Lookup(salesChannelCode),
                             OrderTypeId=Lookup(MAP point.PointType TO OrderTypeCode),
                             BranchId=session.point.BranchId, Status='Open')

    ADD items TO order.OrderItems   -- كل الطلبات بنفس الجلسة تنضم لنفس الفاتورة
    CalculateOrderTotals(order)     -- الدالة 3.2 الأصلية
```

### 4.8 تأكيد طلب QR (يحترم قرار الدفع المسبق لكل فرع/قناة)

```
FUNCTION ConfirmQrOrder(orderId):
    order = LoadOrder(orderId)
    channelConfig = BranchSalesChannelAvailability 
                    WHERE BranchId=order.BranchId AND SalesChannelId=order.SalesChannelId

    IF channelConfig.RequiresPrepayment = TRUE:
        totalPaid = SUM(OrderPayments.Amount WHERE OrderId=orderId)
        IF totalPaid < order.GrandTotal:
            THROW "يجب إتمام الدفع الإلكتروني قبل إرسال الطلب للمطبخ"

    UPDATE order SET Status = 'Sent'
    CalculateTheoreticalConsumption(order.Items)    -- الدالة 3.3 الأصلية
    PrintOrderTickets(orderId)                       -- الدالة 4.2 أعلاه
```

### 4.9 إغلاق الجلسة عند تسوية الفاتورة (مع إعادة توليد الرمز لمنع التداخل)

```
FUNCTION CloseOrderingSession(sessionId):
    order = Orders WHERE OrderingSessionId = sessionId
    IF order.Status NOT IN ('Paid', 'Closed'):
        THROW "لا يمكن إغلاق الجلسة قبل تسوية الفاتورة بالكامل"
    UPDATE OrderingSessions SET Status='Closed', ClosedAt=NOW()

    point = OrderingPoints WHERE Id = session.OrderingPointId
    UPDATE point SET QrCodeToken = GenerateNewUniqueToken()
    -- إلزامي: يمنع أي شخص احتفظ بصورة الـ QR القديم من الطلب بالغلط على العائلة/الزبون التالي لنفس الطاولة/الممر
```

### 4.10 تحويل الطلب لنقطة أخرى (صلاحية مدير الفرع فقط)

```
FUNCTION TransferOrder(orderId, newOrderingPointId, userId, notes):
    IF NOT UserHasPermission(userId, 'CanTransferOrder'):   -- صلاحية مقيّدة، عادة مدير الفرع فقط
        THROW "صلاحية غير كافية لتحويل الطلب"

    order = Orders WHERE Id = orderId
    IF order.Status IN ('Paid', 'Closed'):
        THROW "لا يمكن تحويل طلب مدفوع/مغلق"

    newPoint = OrderingPoints WHERE Id = newOrderingPointId AND IsActive = TRUE
    newSession = OrderingSessions WHERE OrderingPointId = newOrderingPointId AND Status='Open'
    IF newSession IS NULL:
        newSession = INSERT OrderingSessions (OrderingPointId=newOrderingPointId, Status='Open', OpenedAt=NOW())

    oldOrderingSessionId = order.OrderingSessionId
    UPDATE order SET OrderingSessionId = newSession.Id

    INSERT OrderEditLogs (OrderId=orderId, UserId=userId, EditType='Transferred',
                           Notes = notes + " من نقطة " + oldOrderingSessionId + " إلى " + newOrderingPointId,
                           AmountDelta=0)
    -- تسجيل كامل بسجل التدقيق: مين حوّل، من وين لوين، ومتى
```

---

## 5. متطلبات جودة واحترافية (Non-Functional — إلزامية لكل Sprint)

1. **Migrations منظمة:** كل تغيير على الـ Schema عبر migration مرقّم وموثّق (لا تعديل يدوي مباشر على Supabase من لوحة التحكم).
2. **Naming Conventions:** موحّدة مع lolat-suwaiq تمامًا (PascalCase للجداول/الأعمدة كما بالملف المرجعي، snake/kebab حسب معيار الـ Frontend الموجود مسبقًا).
3. **RTL/i18n:** كل نص جديد (فئات، إشعارات، تقارير) يجب أن يدعم `NameAr`/`NameEn` كما بقية النظام — لا نصوص Hard-coded بلغة واحدة.
4. **الصلاحيات (Permissions):** أي دالة تلمس بيانات مالية أو تعدّل بعد الإغلاق (`EditClosedOrder`, `RecordOrderPayment` للدَين، `CloseCashShift`) **يجب** أن تمر بفحص صلاحية صريح قبل التنفيذ — لا استثناءات.
5. **الاختبارات:** كل دالة حسابية (القسم 3 و4) تحتاج Unit Tests تغطي: الحالة الطبيعية، حالة القيم الصفرية/الفارغة، وحالة تجاوز الصلاحية.
6. **لا Hard-coding لأي فئة/ميزة:** أي Pull Request يضيف منطق خاص بـ "Offers" أو "Kids Meal" أو أي فئة بالاسم مباشرة بالكود (Hard-coded Category Name) **يُرفض** — يجب أن يمر عبر `Categories`/`CategoryBranchAvailability` العامة كما بالقسم 2.
7. **الطباعة:** أي اختبار طباعة يتم على بيئة حقيقية (كما هو مخطط بـ Sprint 9 بالملف الأصلي) — لا Mock فقط.
8. **مراجعة الكود:** كل Sprint ينتهي بمراجعة مقابل Acceptance Criteria المكتوبة أدناه بدقة — لا "يعمل تقريبًا".

---

## 6. خطة السبرنتات المُحدَّثة (فوق خطة الملف الأصلي — القسم 4)

> السبرنتات 0 إلى 5 والسبرنت 8 (بنسخته المُبسّطة) و9 و10 **تبقى كما بالملف الأصلي بدون تغيير في نطاقها**. التعديلات والإضافات فقط:

### Sprint 2 (تعديل بسيط) — المنتجات والفئات والكومبوهات
- إضافة `CategoryBranchAvailability` مع شاشة تفعيل/تعطيل الفئة لكل فرع.
- **Acceptance:** إنشاء فئة "Offers" وفئة "Kids Meal" بدون أي كود إضافي، وتعطيل إحداهما بفرع معيّن فقط دون التأثير على باقي الفروع.

### Sprint 6 (تعديل) — المخزون: الوصفات والاستهلاك النظري
- إضافة `Warehouses`, `UnitsOfMeasure`, `InventoryTransactionReasons` قبل تفعيل دالة `CalculateTheoreticalConsumption`.
- **Acceptance:** فرع فيه أكثر من مخزن واحد، وحركة شراء مسجّلة بسبب `PURCHASE_IN` محدد من قائمة أسباب مرجعية.

### Sprint 8 (تأكيد النطاق) — Car Pickup + Feature Flags
- **بدون مراحل Drive-Thru** — فقط `CarPlateNumber` + Feature Flag كما بالملف الأصلي حرفيًا. أي اقتراح لمراحل إضافية يُرفض هذا السبرنت.

### Sprint 9 (تعديل) — الطباعة المتكاملة
- دمج `PrintReceipt` (فاتورة العميل) **و** `PrintOrderTickets` (تذاكر المطبخ حسب `PrinterSection`) معًا.
- **Acceptance:** طلب فيه صنف مشروبات وصنف ساخن يطبع تذكرتين منفصلتين على طابعتين مختلفتين + فاتورة عميل واحدة — كل ذلك ESC/POS بدون أي شاشة رقمية.

### **Sprint 9.5 (جديد) — الدفع وتعديل الفواتير**
- `PaymentMethods`, `OrderPayments`, `OrderEditLogs` + الدوال 4.3 و4.4.
- شاشة دفع تدعم Split Payment، وشاشة "Edit Sales" فيها سجل تعديلات (Previous Edits) مطابق لمفهوم Audit Trail.
- **Acceptance:** دفع طلب بكاش+بطاقة معًا يُغلق الطلب بشكل صحيح؛ محاولة اعتماد دفع "دَين" بدون صلاحية تُرفض؛ تعديل فاتورة مغلقة يُسجَّل بسجل Edit Logs مع المبلغ والمستخدم.

### **Sprint 9.75 (جديد) — إغلاق الصندوق النقدي**
- `CashShifts`, `CashCounts` + دالة 4.5 (`CloseCashShift`).
- شاشة فتح/إغلاق وردية، عدّ نقدية بسيط (بدون تقليد تصميم أي نظام مرجعي)، تقرير فروقات لكل وردية.
- **Acceptance:** فتح وردية برصيد افتتاحي، بيع عدة طلبات كاش، إغلاق الوردية يُظهر Expected/Counted/Variance صحيحة.

### Sprint 10 (تعديل) — التقارير ولوحات المتابعة
- إضافة تقرير فروقات الصناديق (من Sprint 9.75) وتقرير تعديلات الفواتير (من Sprint 9.5) للوحة التحكم الرئيسية، بجانب ما هو مخطط أصلًا.

### **Sprint 9.9 (جديد) — قنوات البيع (Sales Channels)**
- `SalesChannels`, `BranchSalesChannelAvailability` + ربطها بـ `Orders.SalesChannelId`.
- شاشة إدارية لتفعيل/تعطيل قناة بيع لكل فرع، وتفعيل/تعطيل خيار "الدفع المسبق إلزامي" لكل قناة/فرع.
- **Acceptance:** إنشاء قناة "استلام سيارة QR" مفعّلة بفرع وغير مفعّلة بفرع آخر، مع خيار دفع مسبق مختلف بين الفرعين.

### Sprint 11 (تفصيل كامل بدل "Stretch" المختصر بالملف الأصلي) — QR Ordering
- `OrderingPoints`, `CarPickupBays`, `OrderingSessions` + الدوال 4.6 إلى 4.9.
- توليد QR لكل طاولة/ممر سيارة من لوحة التحكم، مع إمكانية إعادة التوليد (Regenerate Token) لأسباب أمنية.
- تطبيق منطق تجميع فاتورة الطاولة (Session) كما بالقسم 3.11 و4.7.
- **Acceptance:** مسح QR طاولة من جوالين مختلفين بنفس الوقت يُنتج فاتورة واحدة مجمّعة؛ تفعيل "دفع مسبق إلزامي" لفرع معيّن يمنع إرسال الطلب للمطبخ قبل اكتمال الدفع؛ مسح QR سيارة يربط الطلب تلقائيًا بنوع "Car Pickup" برقم الممر الصحيح دون إدخال يدوي.

### Sprint 12 — كما بالملف الأصلي بدون تغيير (UAT والنشر).

---

## 7. الأولوية: Must-Have مقابل Good-to-Have

> **قاعدة إلزامية للمُنفِّذ:** رتّب تنفيذ السبرنتات بحيث تُنجَز كل بنود **Must-Have** أولًا قبل الانتقال لأي بند Good-to-Have، بغض النظر عن ترقيم السبرنتات بالقسم 7. لا يبدأ العمل على أي Good-to-Have قبل اكتمال جميع Must-Have واعتمادها.

| # | الميزة | التصنيف |
|---|---|---|
| 1 | الفروع + القاعات + الطاولات + Feature Flags | **Must-Have** |
| 2 | الفئات/المنتجات/الكومبوهات الديناميكية (Offers وKids Meal كفئات عادية) | **Must-Have** |
| 3 | الإضافات (Modifiers) | **Must-Have** |
| 4 | إدارة الطلبات الأساسية (Dine-in/Takeaway/Car Pickup) | **Must-Have** |
| 5 | الإلغاء + سجل التدقيق | **Must-Have** |
| 6 | المخزون: الوصفات (BOM) + الاستهلاك النظري | **Must-Have** |
| 7 | مخازن متعددة + أسباب حركة مخزون + UOM كجدول مرجعي | **Must-Have** |
| 8 | الجرد الفعلي ومقارنة الفروقات | **Must-Have** |
| 9 | Car Pickup (حقل رقم لوحة فقط، بدون مراحل) | **Must-Have** |
| 10 | الطباعة ESC/POS + توجيه حسب القسم (مطبخ/مشروبات) | **Must-Have** |
| 11 | الدفع المتعدد (Split Payment) + الدَين مع اعتماد | **Must-Have** |
| 12 | تعديل الفواتير بعد الإغلاق + سجل تدقيق | **Must-Have** |
| 13 | إغلاق الصندوق النقدي (Cash Shift) | **Must-Have** |
| 14 | قنوات البيع (Sales Channels) + تفعيل لكل فرع | **Must-Have** |
| 15 | QR Ordering — البنية التحتية: `OrderingPoints`/`OrderingSessions`، عزل الجلسات (منع تداخل عائلتين)، إعادة توليد الرمز عند الإغلاق | **Must-Have** (أمان تشغيلي أساسي، وليس تحسينًا) |
| 16 | تحويل الطلب من مدير الفرع (`TransferOrder`) مع سجل تدقيق | **Must-Have** (لنفس السبب أعلاه) |
| 17 | QR Ordering — واجهة الطلب الذاتي الكاملة من جوال الزبون | Good-to-Have |
| 18 | مهلة إغلاق تلقائي للجلسة المتروكة (Idle Timeout) | Good-to-Have (مبدئيًا: إغلاق يدوي من الكاشير يكفي) |
| 19 | تكامل تطبيقات التوصيل الخارجية كقناة فعلية عبر API | Good-to-Have |
| 20 | تقارير/لوحات تحكم متقدمة أبعد من الأساسية | Good-to-Have |
| 21 | Kitchen Display System (KDS) | **مستبعد كليًا** هذه المرحلة (القسم 1، بند 3) |
| 22 | Drive-Thru بمراحل متعددة | **مستبعد كليًا** هذه المرحلة (القسم 1، بند 4) |

> **ملاحظة تنفيذية مهمة:** البند 15 و16 (البنية التحتية للـ QR وعزل الجلسات والتحويل) هي Must-Have حتى لو واجهة الطلب الذاتي الكامل للزبون (البند 17) تأجّلت — يعني تُبنى الجداول والدوال (`OrderingPoints`, `OrderingSessions`, `TransferOrder`, إعادة توليد الرمز) الآن ضمن السبرنتات الأساسية، لكن شاشة "الزبون يطلب من جواله مباشرة" ممكن تُطوَّر لاحقًا إذا احتاج الفريق تسريع الإطلاق.

---

## 8. قائمة "ممنوع" صريحة (لتفادي الانحراف أثناء التنفيذ)

- ❌ ممنوع أي جدول/كود خاص باسم "Offers" أو "KidsMeal" حرفيًا — يمر عبر `Categories` العامة فقط.
- ❌ ممنوع أي مفهوم "Kitchen Display / KDS" هذه المرحلة.
- ❌ ممنوع أي مراحل Drive-Thru متعددة (Order Point/Pay Point/Pickup Point) هذه المرحلة.
- ❌ ممنوع تقليد تصميم/واجهة أي نظام مرجعي (Billbox أو غيره) — الواجهات تتبع التصميم الاحترافي المعتمد من lolat-suwaiq فقط.
- ❌ ممنوع تعديل Auth/Roles الأساسية الموروثة من lolat-suwaiq دون طلب صريح منفصل.
- ❌ ممنوع ربط QR مباشرة بالطاولة (`Tables.QrCodeToken`) — المصدر الوحيد للحقيقة هو `OrderingPoints.QrCodeToken` الموحّد (القسم 3.10).
- ❌ ممنوع إنشاء طلب منفصل لكل شخص بنفس جلسة الطاولة المفتوحة — يجب أن تنضم كل الطلبات لنفس `Order` عبر `OrderingSessionId` (القسم 3.11).
- ❌ ممنوع فتح جلسة `OrderingSessions` جديدة على نقطة عندها جلسة `Open` قائمة بالفعل (القيد الفريد بالقسم 3.11) — وممنوع تخطي إعادة توليد `QrCodeToken` عند إغلاق الجلسة (القسم 4.9)، لأن هذا يكسر ضمان عدم تداخل عائلتين على نفس الطاولة.
- ❌ ممنوع تحويل طلب (`TransferOrder`) بدون صلاحية `CanTransferOrder` وبدون تسجيله بـ `OrderEditLogs` (القسم 4.10).

---

## 9. قرارات ما زالت معلّقة (تحتاج حسمًا قبل Sprint 9.5/9.75 تحديدًا)

1. `CanEditClosedOrder` و`CanApproveDebtPayment` — تحتاج تعريف دقيق ضمن مصفوفة الأدوار/الصلاحيات الموروثة.
2. هل `CategoryBranchAvailability` تكون Fail-open (متاح افتراضيًا) أم Fail-closed (غير متاح حتى يُفعَّل صراحة) لكل فرع جديد؟ — القسم 3.1 اقترح Fail-open كإعداد مبدئي قابل للنقاش.
3. حد الفروقات المقبول للصندوق النقدي (Variance Threshold) — يشابه `IngredientVarianceThreshold` المذكور بالملف الأصلي، يحتاج قيمة موحدة أو حسب الفرع.
4. **مهلة إغلاق الجلسة التلقائي (Idle Timeout) لجلسات QR** — إذا الفاتورة ما اتسوّت خلال مدة معينة (مثلاً 90 دقيقة) هل تُغلق الجلسة تلقائيًا وتُحوَّل للكاشير للمتابعة يدويًا؟ يحتاج رقم محدد.
5. **القيمة الافتراضية لـ `RequiresPrepayment`** عند إنشاء فرع/قناة جديدة (Fail-open = بدون دفع مسبق، أو Fail-closed = دفع مسبق إلزامي حتى يُغيَّر) — يحتاج قرار تشغيلي واضح لتفادي ثغرة تشغيلية بفرع جديد لم يُهيَّأ بعد.
