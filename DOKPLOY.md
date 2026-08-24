# نشر لولاة السويق على Dokploy

هذا الإعداد مخصص للدومين `lolat.almutasim.site`. يتولى Dokploy توجيه الدومين
وإصدار شهادة HTTPS، لذلك لا نُشغّل Certbot أو reverse proxy إضافيًا داخل المشروع.

## 1. DNS

أضف سجل `A` في DNS:

- الاسم: `lolat`
- القيمة: عنوان IP العام لسيرفر Dokploy

تأكد أن `lolat.almutasim.site` يشير إلى السيرفر قبل تفعيل HTTPS.

## 2. إنشاء المشروع في Dokploy

1. ارفع المشروع إلى مستودع Git خاص أو عام.
2. من Dokploy أنشئ **Project** ثم **Compose**.
3. اربط مستودع Git والفرع المطلوب.
4. اختر مسار Compose: `docker-compose.dokploy.yml`.

## 3. متغيرات البيئة

أضف القيم التالية في تبويب Environment داخل Dokploy (لا ترفع ملف `.env` إلى Git):

```env
SUPABASE_DB_CONNECTION=Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true
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

أنشئ `JWT_SECRET` على السيرفر بالأمر:

```bash
openssl rand -base64 64
```

## 4. الدومين وHTTPS

داخل إعدادات خدمة `web` في Dokploy:

- Domain: `lolat.almutasim.site`
- Container port: `80`
- Path: `/`
- HTTPS / Let's Encrypt: مفعّل

لا تضف دومينًا عامًا لخدمة `api`؛ هي متاحة داخليًا فقط، وخدمة `web` تمرّر
`/api/*` و`/uploads/*` إليها من نفس الدومين.

## 5. النشر والتحقق

اضغط **Deploy** ثم راقب سجلات الخدمتين. بعد نجاح البناء افتح:

- `https://lolat.almutasim.site`
- `https://lolat.almutasim.site/api/auth/login` (طلب GET يعيد 405 وهذا يؤكد وصول المسار إلى API)

في أول تشغيل، `RUN_MIGRATIONS_ON_STARTUP=true` يطبق migrations ويضيف حساب
التهيئة إذا كانت قاعدة البيانات فارغة. غيّر كلمة مرور حساب `admin` الافتراضي
مباشرة بعد أول تسجيل دخول.

الملفات المرفوعة ومفاتيح تشفير إعدادات الذكاء الاصطناعي محفوظة في Docker
volumes، لذلك لا تضيع عند إعادة النشر.
