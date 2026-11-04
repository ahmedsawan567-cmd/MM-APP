# M M Application - Loyalty API (نظام ولاء المتاجر والمول)

مشروع الباك إند (Backend) المتكامل لمنظومة ونظام ولاء المتاجر والمول، مبني باستخدام **ASP.NET Core Web API (.NET 9)** مع **Entity Framework Core** و **SQL Server** و **JWT Authentication** وفق معمارية **Clean & Modular Architecture**.

---

## 🏛️ هيكل المشروع (Project Architecture)

```
MMApp/
├── MMApplication.sln
├── src/
│   ├── MMApplication.Domain/                  # الكيانات والتعدادات وقواعد الأعمال
│   │   ├── Entities/ (Customer, CustomerWallet, Invoice, Reward, RewardRedemption, PointTransaction)
│   │   └── Enums/ (CustomerRole, InvoiceStatus, RedemptionStatus, PointTransactionType)
│   │
│   ├── MMApplication.Application/             # الـ DTOs والواجهات
│   │   ├── DTOs/ (AuthDtos, InvoiceDtos, ExtractedReceiptDto, RewardDtos, WalletDtos, AdminDtos)
│   │   └── Interfaces/ (IJwtTokenService, IPasswordHasherService, IReceiptOcrService, IFileStorageService)
│   │
│   ├── MMApplication.Infrastructure/          # قاعدة البيانات والخدمات والترحيلات
│   │   ├── Data/ (AppDbContext, Configurations, Migrations, DataSeeder)
│   │   └── Services/ (JwtTokenService, PasswordHasherService, MockReceiptOcrService, AzureDocumentIntelligenceOcrService, OpenAiVisionOcrService, LocalFileStorageService)
│   │
│   └── MMApplication.Api/                     # واجهة الـ Web API و Swagger
│       ├── Controllers/ (AuthController, InvoicesController, RewardsController, WalletController, AdminController, CustomersController)
│       └── Program.cs & appsettings.json
│
└── tests/
    └── MMApplication.Tests/                  # حزمة الاختبارات الشاملة (43 اختباراً)
```

---

## 🧭 خريطة الـ Endpoints في النظام:

### 1. المصادقة والحسابات (`/api/auth`):
- `POST /api/auth/register`: إنشاء حساب عميل جديد ومحفظته فوراً.
- `POST /api/auth/login`: تسجيل الدخول وإصدار توكن JWT.
- `GET /api/auth/me`: استعلام الملف الشخصي ورصيد المحفظة (`[Authorize]`).

### 2. الفواتير والتعرف الذكي (`/api/invoices`):
- `POST /api/invoices/upload`: رفع صورة الفاتورة، استخراج البيانات، احتساب النقاط وتحديث المحفظة (`[Authorize]`).
- `GET /api/invoices`: استعراض الفواتير (`[Authorize]`).
- `GET /api/invoices/{id}`: استعلام فاتورة محددة (`[Authorize]`).

### 3. المكافآت واستبدال النقاط (`/api/rewards`):
- `GET /api/rewards`: استعراض كتالوج المكافآت والعروض المتاحة.
- `GET /api/rewards/{id}`: تفاصيل مكافأة معينة.
- `POST /api/rewards`: إضافة مكافأة جديدة (`[Authorize(Roles = "Admin")]`).
- `POST /api/rewards/{id}/redeem`: استبدال النقاط وإصدار قسيمة خصم بكود فريد `MM-XXXXXX` (`[Authorize]`).

### 4. المحفظة والقسائم (`/api/wallet`):
- `GET /api/wallet/summary`: ملخص المحفظة والنقاط والقسائم النشطة (`[Authorize]`).
- `GET /api/wallet/transactions`: سجل حركات النقاط التفصيلي (`[Authorize]`).
- `GET /api/wallet/my-vouchers`: قسائم وكوبونات العميل (`[Authorize]`).
- `POST /api/wallet/vouchers/{code}/validate-and-use`: فحص وتطبيق القسيمة عند الكاشير (`[Authorize(Roles = "Admin,Merchant")]`).

### 5. لوحة تحكم الإدارة (`/api/admin` - `[Authorize(Roles = "Admin")]`):
- `GET /api/admin/dashboard/metrics`: إجمالي المبيعات، العملاء، الفواتير، والنقاط المصدرة والمستبدلة.
- `GET /api/admin/dashboard/top-merchants`: المتاجر الأكثر مبيعات وفواتير.
- `GET /api/admin/dashboard/recent-activity`: خلاصة أحدث العمليات.
- `GET /api/admin/invoices/pending`: الفواتير المعلّقة للمراجعة.
- `POST /api/admin/invoices/{id}/approve`: اعتماد فاتورة معلّقة وإضافة نقاطها.
- `POST /api/admin/invoices/{id}/reject`: رفض فاتورة معلّقة مع ذكر السبب.
- `POST /api/admin/customers/{id}/adjust-points`: تعديل رصيد النقاط إدارياً (إضافة أو خصم).
- `GET /api/admin/customers`: إدارة وبحث العملاء.

---

## 🚀 كيفية التشغيل (How to Run)

```bash
# تشغيل حزمة الاختبارات الآلية الكاملة
dotnet test

# تشغيل الـ API
dotnet run --project src/MMApplication.Api/MMApplication.Api.csproj
```

- لوحة التحكم: **`http://localhost:5000/AdminDashboard`**
- توثيق الـ API (Swagger): **`http://localhost:5000/swagger`**

---

## 🔑 الحسابات الافتراضية الجاهزة للاختبار:

| الدور | رقم الهاتف | كلمة المرور | الرصيد / الغرض |
| :--- | :--- | :--- | :--- |
| **Customer** | `0912345678` | `Customer123!` | 150 نقطة (جاهز لرفع الفواتير والاستبدال) |
| **Merchant** | `0988888888` | `Merchant123!` | لفحص وتطبيق القسائم في المحل |
| **Admin** | `0900000000` | `Admin123!` | لوحة القيادة ومراجعة الفواتير وإدارة المكافآت |

> حسابات لوحة التحكم التجريبية (تظهر في صفحة الدخول في بيئة التطوير فقط): `USR-MOSTAFA` / `Admin123!` (المدير العام)، `USR-MOURAD` / `Auditor123!` (مدقق)، `USR-KADI-MGR` / `Manager123!` (مدير متجر). **غيّرها قبل التشغيل الفعلي.**

---

## 🤖 قراءة الفواتير بالذكاء الاصطناعي (Claude)

تُقرأ صور الفواتير (JPG / PNG / WEBP / PDF) بـ Claude من Anthropic: المتجر، رقم الفاتورة، التاريخ، المبلغ، الأصناف، ومطابقة المتجر مع متاجر المول، مع رصد علامات التلاعب.

1. أنشئ مفتاح API من https://console.anthropic.com
2. احفظه على جهازك (لا تكتبه في `appsettings.json`):
   ```bash
   cd src/MMApplication.Api
   dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
   ```
   أو على الخادم كمتغير بيئة: `ANTHROPIC_API_KEY`.
3. أعد تشغيل الـ API. ستظهر في صفحة **مراجعة الفواتير** عبارة "القراءة بواسطة Claude".

بدون مفتاح يعمل محرك تجريبي وهمي (للتطوير فقط)، وخارج بيئة التطوير تُحوّل كل فواتيره للمراجعة اليدوية ولا تُمنح نقاط تلقائياً.

**متى تُعتمد الفاتورة تلقائياً؟** إذا كانت فاتورة شراء حقيقية، من متجر داخل المول، بثقة قراءة ≥ 85%، بدون ملاحظات تلاعب، بتاريخ حديث، بالدينار الليبي، وبمبلغ أقل من 3000 د.ل. غير ذلك تذهب لشاشة **مراجعة الفواتير**. الحدود قابلة للتعديل في قسم `InvoiceReview` في `appsettings.json`.

## 🖥️ لوحة التحكم

| الصفحة | الوظيفة |
| :--- | :--- |
| الرئيسية | مؤشرات حقيقية: المبيعات، الفواتير، النقاط، آخر 14 يوماً |
| التقارير | تقارير حسب الفترة والمتجر + تصدير Excel |
| مراجعة الفواتير | صورة الفاتورة بجانب ما قرأه الذكاء الاصطناعي، اعتماد/رفض/تصحيح، مختبر قراءة الفواتير |
| المكافآت والقسائم | إدارة كتالوج المكافآت، والتحقق من قسائم العملاء عند الدفع |

ملفات الواجهة (Tailwind، الأيقونات، الرسوم البيانية، الخط) تُخدم محلياً من `wwwroot/lib`. عند تعديل الصفحات وإضافة أصناف Tailwind جديدة أعد بناءها:
```bash
cd src/MMApplication.Api/frontend
npm install
npm run build
```

## 📱 تطبيق الجوال (Flutter)

```bash
cd almaraa_mall_flutter
flutter run                                                        # محاكي أندرويد (يتصل بـ http://10.0.2.2:5000)
flutter run --dart-define=API_BASE_URL=http://192.168.1.X:5000/api  # هاتف حقيقي على نفس الشبكة
```

## ⚠️ قبل التشغيل الفعلي
- قاعدة البيانات حالياً **مؤقتة في الذاكرة** (تُمسح عند إعادة التشغيل) — يجب ربطها بـ Supabase/PostgreSQL.
- غيّر `JwtSettings:Secret` وكلمات مرور الحسابات التجريبية، وضعها عبر user-secrets أو متغيرات البيئة.
