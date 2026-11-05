using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MMApplication.Api.Controllers;
using MMApplication.Api.DTOs;
using MMApplication.Api.Services;
using MMApplication.Application.DTOs;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

namespace MMApplication.Tests;

public class NotificationFlowTests
{
    private class FixedOcr(ExtractedReceiptDto r) : IReceiptOcrService
    {
        public Task<ExtractedReceiptDto> ExtractReceiptDataAsync(Stream s) => Task.FromResult(r);
    }

    private class NoStorage : IFileStorageService
    {
        public Task<string> SaveFileAsync(Stream s, string f, string d) => Task.FromResult("/uploads/x.jpg");
        public Task<bool> DeleteFileAsync(string p) => Task.FromResult(true);
    }

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant { TenantId = "KADI", NameAr = "كافي كادي", NameEn = "Kadi", Category = "مطاعم", IsActive = true });
        db.Customers.Add(new Customer { CustomerId = "CUST-A", FullName = "سارة", PhoneNumber = "0910000001", MembershipNumber = "MM-100001" });
        db.CustomerWallets.Add(new CustomerWallet { CustomerId = "CUST-A", BalanceType = BalanceType.POINTS });
        db.SystemUsers.AddRange(
            new SystemUser { UserId = "ADMIN", FullName = "مدير", Role = SystemUserRole.SUPER_ADMIN, IsActive = true, PasswordHash = "x" },
            new SystemUser { UserId = "AUD", FullName = "مدقق", Role = SystemUserRole.FRAUD_AUDITOR, IsActive = true, PasswordHash = "x" },
            new SystemUser { UserId = "OLD", FullName = "معطل", Role = SystemUserRole.SUPER_ADMIN, IsActive = false, PasswordHash = "x" },
            new SystemUser { UserId = "CASH", FullName = "كاشير", Role = SystemUserRole.CASHIER, IsActive = true, PasswordHash = "x", TenantId = "KADI" });
        await db.SaveChangesAsync();
    }

    private static InvoiceUploadRequest Upload()
    {
        var bytes = Encoding.UTF8.GetBytes("img");
        return new InvoiceUploadRequest
        {
            CustomerId = "CUST-A",
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "r.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" }
        };
    }

    private static ExtractedReceiptDto Receipt(double confidence) => new()
    {
        MerchantName = "كافي كادي", MatchedTenantId = "KADI", InvoiceNumber = "KC-" + Guid.NewGuid().ToString("N")[..6],
        InvoiceDate = DateTime.UtcNow, TotalAmount = 120m, Confidence = confidence,
    };

    [Fact]
    public async Task InvoiceNeedingReview_NotifiesActiveReviewers_AndTheCustomer()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var ctrl = new InvoicesController(db, new FixedOcr(Receipt(0.5)), new NoStorage(), new FlexibleLoyaltyEvaluationService(db), NullLogger<InvoicesController>.Instance);
        await ctrl.UploadInvoice(Upload());

        var staff = db.Notifications.Where(n => n.RecipientType == Notification.ToStaff).Select(n => n.RecipientId).ToList();
        Assert.Equal(["ADMIN", "AUD"], staff.OrderBy(x => x));
        Assert.Contains(db.Notifications, n => n.RecipientId == "CUST-A" && n.Type == "INVOICE_IN_REVIEW");

        // اعتماد بعد المراجعة → إشعار للعميل بالنقاط
        var invoice = db.Invoices.Single();
        var review = new InvoiceReviewService(db, new FlexibleLoyaltyEvaluationService(db), NullLogger<InvoiceReviewService>.Instance);
        Assert.True((await review.ApproveAsync(invoice.InvoiceId, "مدقق")).Success);
        Assert.Contains(db.Notifications, n => n.RecipientId == "CUST-A" && n.Type == "INVOICE_APPROVED");
    }

    [Fact]
    public async Task AutoApprovedInvoice_NotifiesCustomerOnly()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var ctrl = new InvoicesController(db, new FixedOcr(Receipt(0.97)), new NoStorage(), new FlexibleLoyaltyEvaluationService(db), NullLogger<InvoicesController>.Instance);
        await ctrl.UploadInvoice(Upload());

        Assert.Equal(InvoiceStatus.APPROVED, db.Invoices.Single().ProcessingStatus);
        Assert.Single(db.Notifications, n => n.RecipientId == "CUST-A" && n.Type == "INVOICE_APPROVED");
        Assert.DoesNotContain(db.Notifications, n => n.RecipientType == Notification.ToStaff);
    }

    [Fact]
    public async Task Broadcast_ReachesActiveCustomersInSegment()
    {
        await using var db = NewDb();
        db.Customers.AddRange(
            new Customer { CustomerId = "G1", FullName = "a", PhoneNumber = "0911000001", LoyaltyTier = "GOLD" },
            new Customer { CustomerId = "B1", FullName = "b", PhoneNumber = "0911000002", LoyaltyTier = "BRONZE" },
            new Customer { CustomerId = "F1", FullName = "c", PhoneNumber = "0911000003", LoyaltyTier = "GOLD", IsFrozen = true });
        await db.SaveChangesAsync();

        Assert.Equal(1, await Notifier.BroadcastAsync(db, "VIP_GOLD", "عرض", "خصم"));
        Assert.Equal(2, await Notifier.BroadcastAsync(db, "ALL", "عرض", "خصم"));
    }

    [Fact]
    public async Task Broadcast_IsRecordedInHistory_AndLegacyRowsAreBackfilled()
    {
        await using var db = NewDb();
        db.Customers.Add(new Customer { CustomerId = "C1", FullName = "a", PhoneNumber = "0911000011" });
        // إشعار جماعي قديم قبل وجود السجل
        var old = DateTime.UtcNow.AddDays(-2);
        db.Notifications.Add(new Notification { RecipientId = "C1", Type = "BROADCAST", Title = "قديم", Body = "x", CreatedAt = old });
        await db.SaveChangesAsync();

        await Notifier.BroadcastAsync(db, "ALL", "جديد", "نص", "المدير");
        await db.SaveChangesAsync();
        var b = Assert.Single(db.NotificationBroadcasts);
        Assert.Equal(("جديد", 1, "المدير"), (b.Title, b.RecipientsCount, b.SentBy));
        Assert.Single(db.Notifications, n => n.BroadcastId == b.Id);

        Assert.Equal(1, await Notifier.BackfillBroadcastsAsync(db));
        Assert.Equal(0, await Notifier.BackfillBroadcastsAsync(db));
        Assert.Contains(db.NotificationBroadcasts, x => x.Title == "قديم" && x.SentAt == old);
    }
}

public class PushDispatchTests
{
    private sealed class FakeGoogle : HttpMessageHandler
    {
        public List<string> Sent { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.RequestUri!.Host == "oauth2.googleapis.com")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "at", expires_in = 3600 }) };
            var body = await req.Content!.ReadAsStringAsync(ct);
            Sent.Add(body);
            Assert.Equal("Bearer at", req.Headers.Authorization!.ToString());
            return body.Contains("dead-token")
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":{\"status\":\"NOT_FOUND\",\"details\":[{\"errorCode\":\"UNREGISTERED\"}]}}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"name\":\"m\"}") };
        }
    }

    private static FirebaseServiceAccount Account()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var json = JsonSerializer.Serialize(new
        {
            type = "service_account",
            project_id = "mm-test",
            client_email = "push@mm-test.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.googleapis.com/token",
        });
        return FirebaseServiceAccount.Parse(json)!;
    }

    [Fact]
    public void ServiceAccount_RejectsIncompleteJson()
    {
        Assert.Null(FirebaseServiceAccount.Parse("{\"project_id\":\"x\"}"));
        Assert.Null(FirebaseServiceAccount.Parse("not json"));
        Assert.Equal("mm-test", Account().ProjectId);
    }

    [Fact]
    public async Task Dispatch_SendsNewNotifications_SkipsOldOnes_AndDropsDeadTokens()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.DeviceTokens.AddRange(
            new DeviceToken { CustomerId = "C1", Token = "good-token" },
            new DeviceToken { CustomerId = "C1", Token = "dead-token" });
        db.Notifications.AddRange(
            new Notification { RecipientId = "C1", Type = "INVOICE_APPROVED", Title = "أُضيفت نقاطك", Body = "+50", Link = "invoice:1" },
            new Notification { RecipientId = "C1", Type = "BROADCAST", Title = "قديم", Body = "x", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new Notification { RecipientId = "C2", Type = "BROADCAST", Title = "بدون جهاز", Body = "x" },
            new Notification { RecipientType = Notification.ToStaff, RecipientId = "ADMIN", Type = "REVIEW_NEEDED", Title = "موظف", Body = "x" });
        await db.SaveChangesAsync();

        var google = new FakeGoogle();
        var sender = new FcmSender(new HttpClient(google), Account(), NullLogger<FcmSender>.Instance);
        Assert.Equal(2, await PushDispatchService.DispatchOnceAsync(db, sender));

        Assert.Equal(2, google.Sent.Count); // جهازان لإشعار واحد
        Assert.Contains("\"channel_id\":\"mm_updates\"", google.Sent[0]);
        Assert.Contains("invoice:1", google.Sent[0]);
        Assert.Single(db.DeviceTokens); // الرمز الميت حُذف
        Assert.NotNull(db.Notifications.Single(n => n.Title == "أُضيفت نقاطك").PushSentAt);
        Assert.NotNull(db.Notifications.Single(n => n.Title == "بدون جهاز").PushSentAt);
        Assert.Null(db.Notifications.Single(n => n.Title == "قديم").PushSentAt);
        Assert.Null(db.Notifications.Single(n => n.Title == "موظف").PushSentAt);

        // لا يُعاد الإرسال
        Assert.Equal(0, await PushDispatchService.DispatchOnceAsync(db, sender));
    }
}

public class NotificationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public NotificationApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "InMemory",
                ["InMemoryDbName"] = "NotifDb_" + Guid.NewGuid(),
            }));
        });
    }

    [Fact]
    public async Task Customer_SeesBroadcast_AndMarksItRead()
    {
        // عميل جديد
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/Auth/register", new { fullName = "عميل الإشعارات", phoneNumber = "0928765432", password = "Secret123!" });
        var auth = (await reg.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        // المدير يرسل إعلاناً من المنصة
        var admin = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await admin.GetStringAsync("/AdminDashboard/Login");
        var token = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        await admin.PostAsync("/AdminDashboard/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["Username"] = "USR-MOSTAFA", ["Password"] = "Admin123!" }));
        var send = await admin.PostAsJsonAsync("/api/superadmin/notifications/send", new { title = "عرض نهاية الأسبوع", body = "نقاط مضاعفة", targetAudience = "ALL" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/Notifications");
        Assert.True(list.GetProperty("unread").GetInt32() >= 1);
        var item = list.GetProperty("items").EnumerateArray().First(i => i.GetProperty("title").GetString() == "عرض نهاية الأسبوع");

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/Notifications/{item.GetProperty("id").GetString()}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/Notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/Notifications/unread-count")).GetProperty("unread").GetInt32());

        // تسجيل جهاز للإشعارات الفورية (مرتين = صف واحد)
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Notifications/device-token", new { token = "fcm-abc", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Notifications/device-token", new { token = "fcm-abc", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Notifications/device-token", new { token = "" })).StatusCode);
        Assert.Equal(1, (await client.DeleteFromJsonAsync<JsonElement>("/api/Notifications/device-token?token=fcm-abc")).GetProperty("removed").GetInt32());

        // سجل الإشعارات المرسلة في المنصة + السحب
        var page = await admin.GetStringAsync("/AdminDashboard/SentNotifications");
        Assert.Contains("عرض نهاية الأسبوع", page);
        var id = Regex.Match(page, @"retract\('([0-9a-f-]{36})'").Groups[1].Value;
        var af = Regex.Match(page, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        var req = new HttpRequestMessage(HttpMethod.Post, $"/AdminDashboard/RetractNotification?id={id}");
        req.Headers.Add("RequestVerificationToken", af);
        Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(req)).StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/Notifications");
        Assert.DoesNotContain(after.GetProperty("items").EnumerateArray(), i => i.GetProperty("title").GetString() == "عرض نهاية الأسبوع");

        // الموظف: جرس المنصة يعمل
        var staff = await admin.GetFromJsonAsync<JsonElement>("/AdminDashboard/NotificationsJson");
        Assert.True(staff.TryGetProperty("unread", out _));
    }
}
