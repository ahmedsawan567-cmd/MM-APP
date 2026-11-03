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

افتح المتصفح على: **`http://localhost:5000/`** للاختبار والتفاعل المباشر عبر **Swagger UI**.

---

## 🔑 الحسابات الافتراضية الجاهزة للاختبار:

| الدور | رقم الهاتف | كلمة المرور | الرصيد / الغرض |
| :--- | :--- | :--- | :--- |
| **Customer** | `0912345678` | `Customer123!` | 150 نقطة (جاهز لرفع الفواتير والاستبدال) |
| **Merchant** | `0988888888` | `Merchant123!` | لفحص وتطبيق القسائم في المحل |
| **Admin** | `0900000000` | `Admin123!` | لوحة القيادة ومراجعة الفواتير وإدارة المكافآت |
