# نشر OFC على Dokploy

هذا الإعداد مخصص للدومين `ofc.almutasim.site`، ويفترض أن Supabase (ذاتية
الاستضافة) منشورة على نفس سيرفر Dokploy كمشروع Compose منفصل. `api` يتصل
بقاعدة البيانات مباشرة عبر شبكة Docker الداخلية لمشروع Supabase (`Host=db`)
بدلاً من الدومين العام، لأن الدومين العام (خلف Cloudflare) لا يمرّر اتصال
Postgres/pooler الخام. `web` ينضم إلى `dokploy-network` ليصله Traefik على
الدومين العام. Dokploy يتولى توجيه الدومين وإصدار شهادة HTTPS تلقائيًا؛ لا
حاجة لـ Certbot أو reverse proxy إضافي داخل المشروع.

## 1. تأكد من اسم شبكة Supabase

`docker-compose.yml` يشير إلى شبكة Supabase الخارجية باسم ثابت:

```yaml
supabase-db:
  name: ofc-supabase-tvmlhw_default
  external: true
```

هذا الاسم يعتمد على اسم مشروع Compose الذي نُشرت به حزمة Supabase ذاتية
الاستضافة في Dokploy (Dokploy يضيف لاحقة عشوائية لكل مشروع). تحقق من الاسم
الفعلي على السيرفر:

```bash
docker network ls | grep supabase
```

إذا اختلف عن `ofc-supabase-tvmlhw_default`، حدّث القيمة في `docker-compose.yml`
قبل النشر (أو إذا أُعيد نشر حزمة Supabase لاحقًا تحت لاحقة مختلفة).

## 2. إنشاء المشروع في Dokploy

1. المستودع مرفوع بالفعل: `https://github.com/Almutasim90/ofc.git`.
2. من Dokploy أنشئ **Project** ثم **Compose** (أو استخدم مشروع OFC الموجود
   إن كان منشأ مسبقًا).
3. اربط المستودع والفرع المطلوب (`main`).
4. مسار Compose: `docker-compose.yml` (وليس `docker-compose.dokploy.yml` —
   هذا الملف قديم ومحذوف الآن لأنه لا يعكس إعداد Supabase ذاتية الاستضافة).

## 3. متغيرات البيئة

أضف القيم التالية في تبويب Environment داخل Dokploy (لا ترفع ملف `.env` إلى
Git):

```env
SUPABASE_DB_CONNECTION=Host=db;Port=5432;Database=postgres;Username=postgres;Password=...;SSL Mode=Disable
SUPABASE_URL=https://<دومين-Kong-الخاص-بحزمة-Supabase>
SUPABASE_SECRET_KEY=sb_secret_...
JWT_SECRET=ضع_هنا_قيمة_عشوائية_طويلة_جداً
JWT_ISSUER=POS.API
JWT_AUDIENCE=POS.Client
RUN_MIGRATIONS_ON_STARTUP=true

SMTP_HOST=
SMTP_PORT=587
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_FROM=
SMTP_ALERT_RECIPIENTS=
```

ملاحظات:

- `SUPABASE_DB_CONNECTION` يستخدم `Host=db` (اسم خدمة Postgres داخل شبكة
  Supabase الداخلية) لأن `api` منضم لتلك الشبكة مباشرة — راجع اسم الخدمة
  الفعلي في `docker-compose.yml` الخاص بحزمة Supabase إذا اختلف.
- `SUPABASE_URL` هو دومين Kong العام لحزمة Supabase (لتخزين الملفات
  المرفوعة عبر Storage)، **ليس** نفس اتصال Postgres.
- أنشئ `JWT_SECRET` على السيرفر بالأمر:

```bash
openssl rand -base64 64
```

## 4. الدومين وHTTPS

داخل إعدادات خدمة `web` في Dokploy:

- Domain: `ofc.almutasim.site`
- Container port: `80`
- Path: `/`
- HTTPS / Let's Encrypt: مفعّل

لا تضف دومينًا عامًا لخدمة `api`؛ هي متاحة داخليًا فقط (على `expose: 8080`
وشبكة `default` المشتركة بين الخدمتين)، وخدمة `web` تمرّر `/api/*` و
`/uploads/*` إليها من نفس الدومين عبر تهيئة nginx داخل حاوية `web`.

تأكد من DNS قبل التفعيل: سجل `A` باسم `ofc` يشير إلى IP سيرفر Dokploy،
ويمكنك التحقق بـ `dig ofc.almutasim.site` من خارج السيرفر.

## 5. النشر والتحقق

قبل كل نشر يغيّر قاعدة البيانات، اتبع بوابات القبول والنسخ الاحتياطي والاسترجاع
والتراجع في [`UAT-ROLLOUT.md`](UAT-ROLLOUT.md). لا يكفي نجاح بناء الحاويات لإعلان
قبول الفرع الحقيقي. احتفظ بنسخة API واحدة فقط في مرحلة التجربة؛ قفل عمليات QR
الحالي داخل العملية ولا يدعم التوسع الأفقي الآمن بعد.

اضغط **Deploy** ثم راقب سجلات الخدمتين. بعد نجاح البناء افتح:

- `https://ofc.almutasim.site`
- `https://ofc.almutasim.site/api/auth/login` (طلب GET يعيد 405 وهذا يؤكد
  وصول المسار إلى API)

في أول تشغيل، `RUN_MIGRATIONS_ON_STARTUP=true` يطبق migrations ويضيف حساب
التهيئة (`admin` / `Admin@12345`) إذا كانت قاعدة البيانات فارغة. غيّر كلمة
مرور حساب `admin` الافتراضي مباشرة بعد أول تسجيل دخول — كلمة المرور موجودة
في الكود المصدري العام (`SeedData.cs`).

الملفات المرفوعة (`api_uploads`) ومفاتيح تشفير إعدادات الذكاء الاصطناعي
(`api_keys`) محفوظة في Docker volumes، لذلك لا تضيع عند إعادة النشر.

## إعادة النشر بعد تعديل الكود

من Dokploy: **Redeploy** (يسحب آخر commit من الفرع المربوط ويعيد البناء).
بما أن `RUN_MIGRATIONS_ON_STARTUP=true`، أي migration جديدة في الفرع تُطبّق
تلقائيًا عند إعادة التشغيل.

لا تضغط **Redeploy** قبل تسجيل معرّف نسخة PostgreSQL احتياطية تم اختبار
استرجاعها. إذا فشل migration أو ظهرت مشكلة تمس المدفوعات أو المخزون، أوقف
الكتابة واتبع قسم Rollback في `UAT-ROLLOUT.md` بدل تشغيل down migration عشوائيًا.
