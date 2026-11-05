using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.DTOs;
using MMApplication.Application.Common;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

/// <summary>أرقام الهواتف وأرقام الفواتير.</summary>
public class IdentifierRulesTests
{
    [Theory]
    [InlineData("0912345678", "0912345678")]
    [InlineData("091 234 5678", "0912345678")]
    [InlineData("+218 91 234 5678", "0912345678")]
    [InlineData("00218912345678", "0912345678")]
    [InlineData("218-91-234-5678", "0912345678")]
    [InlineData("912345678", "0912345678")]
    [InlineData("٠٩١٢٣٤٥٦٧٨", "0912345678")]
    [InlineData("+44 7911 123456", "+447911123456")]
    public void Phone_IsNormalized(string input, string expected) => Assert.Equal(expected, PhoneNumbers.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0812345678")]
    [InlineData("09123abc78")]
    public void Phone_InvalidIsRejected(string input) => Assert.Null(PhoneNumbers.Normalize(input));

    [Theory]
    [InlineData("INV-00123", "INV00123")]
    [InlineData("inv 00123", "INV00123")]
    [InlineData("#000123", "123")]
    [InlineData("٠٠١٢٣", "123")]
    public void InvoiceKey_IsNormalized(string input, string expected) => Assert.Equal(expected, InvoiceNumbers.Key(input));

    [Fact]
    public void InvoicePattern_FromExample_AcceptsSameShapeOnly()
    {
        var pattern = InvoiceNumbers.PatternFromExample("INV-2024-000123");
        Assert.True(InvoiceNumbers.MatchesPattern("INV-2025-000999", pattern));
        Assert.True(InvoiceNumbers.MatchesPattern("inv-2025-1000123", pattern)); // العدّاد كبر
        Assert.False(InvoiceNumbers.MatchesPattern("2025-000999", pattern));
        Assert.False(InvoiceNumbers.MatchesPattern("REC-2025-000999", pattern));
        Assert.Null(InvoiceNumbers.MatchesPattern("X", "([")); // نمط غير صالح لا يكسر القراءة
    }

    [Fact]
    public void ProfileAnalyzer_Parse_BuildsPatternFromExample()
    {
        var s = ClaudeInvoiceProfileAnalyzer.Parse("""
            {"is_receipt":true,"printed_name":"كافي كادي","other_names":["Kadi Cafe"],"tax_number":"LY-1234",
             "phone":null,"invoice_number_label":"رقم الفاتورة","invoice_number_example":"KC-000451",
             "total_label":"الإجمالي","date_format":"DD/MM/YYYY","currency":"LYD","layout_notes":"شعار في الأعلى"}
            """);
        Assert.Equal("KC-000451", s.InvoiceNumberExample);
        Assert.Equal(["Kadi Cafe"], s.OtherNames);
        Assert.True(InvoiceNumbers.MatchesPattern("KC-000452", s.InvoiceNumberPattern));
    }
}

/// <summary>هوية العميل وتعديل بياناته من التطبيق.</summary>
public class CustomerAccountApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CustomerAccountApiTests(WebApplicationFactory<Program> factory)
    {
        var db = "CustomerAccountDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = db,
                ["SeedData"] = "None"
            }));
        });
    }

    private async Task<(HttpClient client, AuthResponseDto auth)> RegisterAsync(string phone, string password = "Secret123!")
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/Auth/register", new { fullName = "عميلة تجربة", phoneNumber = phone, password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }

    [Fact]
    public async Task SamePhoneWrittenDifferently_CannotOpenSecondAccount()
    {
        var (_, first) = await RegisterAsync("+218 92 111 2233");
        Assert.Equal("0921112233", first.PhoneNumber);
        Assert.Matches(@"^MM-\d{6}$", first.MembershipNumber!);

        var again = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/register",
            new { fullName = "شخص آخر", phoneNumber = "0921112233", password = "Other123!" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // الدخول يقبل أي صيغة للرقم
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/login", new { phoneNumber = "00218921112233", password = "Secret123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task MembershipNumbers_AreUniqueAndSequential()
    {
        var (_, a) = await RegisterAsync("0923000001");
        var (_, b) = await RegisterAsync("0923000002");
        var na = int.Parse(a.MembershipNumber![3..]);
        var nb = int.Parse(b.MembershipNumber![3..]);
        // أصناف الاختبار الأخرى تسجّل عملاء بالتوازي في نفس القاعدة، فنتحقق من التفرّد والترتيب
        Assert.True(nb > na);
    }

    [Fact]
    public async Task Customer_UpdatesProfile_ChangesPassword_AndDeletesAccount()
    {
        var (client, auth) = await RegisterAsync("0924000001");

        var update = await client.PutAsJsonAsync("/api/Auth/me",
            new { fullName = "سارة محمد", email = "Sara@Example.com", birthDate = "1995-04-12", gender = "F" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/Auth/me");
        Assert.Equal("سارة محمد", me.GetProperty("fullName").GetString());
        Assert.Equal("sara@example.com", me.GetProperty("email").GetString());
        Assert.Equal(auth.MembershipNumber, me.GetProperty("membershipNumber").GetString());

        var wrong = await client.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = "nope", newPassword = "NewSecret123" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var changed = await client.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = "Secret123!", newPassword = "NewSecret123" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/login", new { phoneNumber = "0924000001", password = "Secret123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var deleted = await client.PostAsJsonAsync("/api/Auth/delete-account", new { password = "NewSecret123" });
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var afterDelete = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/login", new { phoneNumber = "0924000001", password = "NewSecret123" });
        Assert.Equal(HttpStatusCode.Unauthorized, afterDelete.StatusCode);

        // الرقم تحرر: يمكن لصاحبه فتح حساب جديد
        await RegisterAsync("0924000001");
    }

    [Fact]
    public async Task Email_MustBeUniqueAcrossCustomers()
    {
        var (a, _) = await RegisterAsync("0925000001");
        var (b, _) = await RegisterAsync("0925000002");
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync("/api/Auth/me", new { fullName = "عميل أول", email = "same@mail.ly" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.PutAsJsonAsync("/api/Auth/me", new { fullName = "عميل ثاني", email = "SAME@mail.ly" })).StatusCode);
    }
}

/// <summary>تعديل العملاء من لوحة التحكم.</summary>
public class AdminCustomerEditingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminCustomerEditingTests(WebApplicationFactory<Program> factory)
    {
        var db = "AdminCustomerDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = db,
                ["SeedData"] = "Demo"
            }));
        });
    }

    private async Task<HttpClient> LoginAsync(string user, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/AdminDashboard/Login");
        var token = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        var res = await client.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Username"] = user, ["Password"] = password
        }));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        return client;
    }

    private static async Task<string> CreateAsync(HttpClient admin, string phone, string? password = null)
    {
        var res = await admin.PostAsJsonAsync("/api/superadmin/customers", new { fullName = "عميل من المكتب", phoneNumber = phone, tier = "BRONZE", password });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("MM-", dto.GetProperty("membershipNumber").GetString());
        return dto.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Admin_EditsAnyField_AndPhoneStaysUnique()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var id = await CreateAsync(admin, "0926000001");
        await CreateAsync(admin, "0926000002");

        var ok = await admin.PutAsJsonAsync($"/api/superadmin/customers/{id}",
            new { fullName = "اسم معدل", phoneNumber = "+218 92 600 0009", tier = "GOLD", email = "edit@mail.ly", birthDate = "1990-01-01", gender = "M" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/superadmin/customers?search=0926000009");
        var c = list.EnumerateArray().Single();
        Assert.Equal("اسم معدل", c.GetProperty("fullName").GetString());
        Assert.Equal("GOLD", c.GetProperty("tier").GetString());
        Assert.Equal("edit@mail.ly", c.GetProperty("email").GetString());

        var dup = await admin.PutAsJsonAsync($"/api/superadmin/customers/{id}", new { fullName = "اسم معدل", phoneNumber = "0926000002", tier = "GOLD" });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Admin_ResetsPassword_CustomerCanLogin()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var id = await CreateAsync(admin, "0927000001");

        var res = await admin.PostAsJsonAsync($"/api/superadmin/customers/{id}/reset-password", new { });
        var password = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("password").GetString();

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/login", new { phoneNumber = "0927000001", password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutActivity_RemovesCustomer()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var id = await CreateAsync(admin, "0928000001");

        var res = await admin.DeleteAsync($"/api/superadmin/customers/{id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.False((await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("anonymized").GetBoolean());
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/superadmin/customers?search=0928000001");
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Auditor_CanEditButNotDeleteOrAdjustPoints()
    {
        var admin = await LoginAsync("USR-MOSTAFA", "Admin123!");
        var id = await CreateAsync(admin, "0929000001");
        var auditor = await LoginAsync("USR-MOURAD", "Auditor123!");

        Assert.Equal(HttpStatusCode.OK, (await auditor.PutAsJsonAsync($"/api/superadmin/customers/{id}", new { fullName = "تعديل المدقق", phoneNumber = "0929000001", tier = "BRONZE" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.DeleteAsync($"/api/superadmin/customers/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync($"/api/superadmin/customers/{id}/adjust-points", new { pointsDelta = 500, reason = "x" })).StatusCode);

        var manager = await LoginAsync("USR-KADI-MGR", "Manager123!");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/superadmin/customers/{id}", new { fullName = "x", phoneNumber = "0929000001" })).StatusCode);
    }
}

/// <summary>رقم الفاتورة يُستخدم مرة واحدة لكل متجر، وبصمة فاتورة المتجر.</summary>
public class DuplicateInvoiceTests
{
    private class FixedOcr(ExtractedReceiptDto result) : IReceiptOcrService
    {
        public ExtractedReceiptDto Result { get; set; } = result;
        public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream imageStream) => Task.FromResult(Result);
    }

    private class NoStorage : IFileStorageService
    {
        public Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName) => Task.FromResult($"/uploads/{folderName}/{fileName}");
        public Task<bool> DeleteFileAsync(string relativeFilePath) => Task.FromResult(true);
    }

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant { TenantId = "KADI", NameAr = "كافي كادي", NameEn = "Kadi Cafe", Category = "مطاعم", IsActive = true });
        foreach (var id in new[] { "CUST-A", "CUST-B" })
        {
            db.Customers.Add(new Customer { CustomerId = id, FullName = id, PhoneNumber = id == "CUST-A" ? "0910000001" : "0910000002" });
            db.CustomerWallets.Add(new CustomerWallet { CustomerId = id, BalanceType = BalanceType.POINTS, CurrentBalance = 0 });
        }
        await db.SaveChangesAsync();
    }

    private static InvoicesController Controller(AppDbContext db, IReceiptOcrService ocr) =>
        new(db, ocr, new NoStorage(), new FlexibleLoyaltyEvaluationService(db), NullLogger<InvoicesController>.Instance);

    private static InvoiceUploadRequest Upload(string customerId)
    {
        var bytes = Encoding.UTF8.GetBytes("img");
        return new InvoiceUploadRequest
        {
            CustomerId = customerId,
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "r.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" }
        };
    }

    private static ExtractedReceiptDto Receipt(string number, string? tax = null) => new()
    {
        MerchantName = "كافي كادي",
        MatchedTenantId = "KADI",
        InvoiceNumber = number,
        TaxNumber = tax,
        InvoiceDate = DateTime.UtcNow,
        TotalAmount = 42m,
        Confidence = 0.97,
    };

    [Fact]
    public async Task SameInvoiceNumber_WrittenDifferently_FromAnotherAccount_IsRejectedAndLogged()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var ocr = new FixedOcr(Receipt("KC-000451"));

        Assert.IsType<OkObjectResult>(await Controller(db, ocr).UploadInvoice(Upload("CUST-A")));

        ocr.Result = Receipt("kc 000451");
        var second = Assert.IsType<ConflictObjectResult>(await Controller(db, ocr).UploadInvoice(Upload("CUST-B")));
        Assert.Contains("DUPLICATE_OTHER", JsonSerializer.Serialize(second.Value));
        Assert.Single(db.Invoices);
        Assert.Contains(db.AuditLogs, l => l.Action == "INVOICE_DUPLICATE_OTHER_ACCOUNT");
    }

    [Fact]
    public async Task SameCustomer_ReuploadingInvoice_IsRejected()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var ocr = new FixedOcr(Receipt("KC-000500"));
        Assert.IsType<OkObjectResult>(await Controller(db, ocr).UploadInvoice(Upload("CUST-A")));
        var again = Assert.IsType<ConflictObjectResult>(await Controller(db, ocr).UploadInvoice(Upload("CUST-A")));
        Assert.Contains("DUPLICATE_SELF", JsonSerializer.Serialize(again.Value));
    }

    [Fact]
    public async Task StoreProfile_Mismatch_SendsInvoiceToReview()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        db.StoreInvoiceProfiles.Add(new StoreInvoiceProfile
        {
            TenantId = "KADI",
            TaxNumber = "LY-1234",
            InvoiceNumberExample = "KC-000451",
            InvoiceNumberPattern = InvoiceNumbers.PatternFromExample("KC-000451"),
        });
        await db.SaveChangesAsync();

        // رقم بنمط مختلف ورقم ضريبي مختلف عن المتجر
        await Controller(db, new FixedOcr(Receipt("99887766", tax: "LY-9999"))).UploadInvoice(Upload("CUST-A"));
        var invoice = db.Invoices.Single();
        Assert.Equal(InvoiceStatus.REVIEW_REQUIRED, invoice.ProcessingStatus);
        Assert.Contains("لا يطابق نمط", invoice.RawAiPayload);
        Assert.Contains("الرقم الضريبي", invoice.RawAiPayload);
    }

    [Fact]
    public async Task StoreProfile_Match_IsAutoApproved()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        db.StoreInvoiceProfiles.Add(new StoreInvoiceProfile { TenantId = "KADI", TaxNumber = "LY 1234", InvoiceNumberPattern = InvoiceNumbers.PatternFromExample("KC-000451") });
        await db.SaveChangesAsync();

        await Controller(db, new FixedOcr(Receipt("KC-000777", tax: "LY-1234"))).UploadInvoice(Upload("CUST-A"));
        Assert.Equal(InvoiceStatus.APPROVED, db.Invoices.Single().ProcessingStatus);
    }

    [Fact]
    public async Task InvoiceNumberKey_IsSetAutomaticallyOnSave()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        db.Invoices.Add(new Invoice { CustomerId = "CUST-A", TenantId = "KADI", InvoiceNumber = "inv-0042" });
        await db.SaveChangesAsync();
        Assert.Equal("INV0042", db.Invoices.Single().InvoiceNumberKey);
    }
}
