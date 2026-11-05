using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMApplication.Domain.Entities;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Infrastructure.Services;

/// <summary>بيانات حساب الخدمة من Firebase (ملف JSON يُحمَّل من إعدادات المشروع).</summary>
public sealed record FirebaseServiceAccount(string ProjectId, string ClientEmail, string PrivateKey, string TokenUri)
{
    /// <summary>
    /// يقرأ حساب الخدمة من الإعدادات (بالترتيب): Firebase:ServiceAccountJson (النص نفسه)،
    /// Firebase:ServiceAccountPath (مسار الملف)، أو متغير GOOGLE_APPLICATION_CREDENTIALS.
    /// </summary>
    public static FirebaseServiceAccount? FromConfiguration(IConfiguration config)
    {
        var json = config["Firebase:ServiceAccountJson"];
        if (string.IsNullOrWhiteSpace(json))
        {
            var path = config["Firebase:ServiceAccountPath"] ?? config["GOOGLE_APPLICATION_CREDENTIALS"];
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            json = File.ReadAllText(path);
        }
        return Parse(json);
    }

    public static FirebaseServiceAccount? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            string? Get(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var projectId = Get("project_id");
            var email = Get("client_email");
            var key = Get("private_key");
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(key)) return null;
            return new FirebaseServiceAccount(projectId, email, key, Get("token_uri") ?? "https://oauth2.googleapis.com/token");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public enum PushResult { Sent, InvalidToken, Failed }

/// <summary>
/// إرسال إشعارات الهاتف عبر Firebase Cloud Messaging (واجهة HTTP v1) بدون مكتبات إضافية:
/// توقيع JWT بمفتاح حساب الخدمة ← رمز وصول OAuth ← إرسال لكل جهاز.
/// </summary>
public class FcmSender
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly HttpClient _http;
    private readonly FirebaseServiceAccount _account;
    private readonly ILogger<FcmSender> _logger;
    private string? _accessToken;
    private DateTime _accessTokenExpires;

    public FcmSender(HttpClient http, FirebaseServiceAccount account, ILogger<FcmSender> logger)
    {
        _http = http;
        _account = account;
        _logger = logger;
    }

    public string ProjectId => _account.ProjectId;

    public async Task<PushResult> SendAsync(string token, string title, string body, IDictionary<string, string>? data = null, string? tag = null, CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(ct);
        var dataNode = new JsonObject();
        foreach (var (k, v) in data ?? new Dictionary<string, string>()) dataNode[k] = v;

        var androidNotification = new JsonObject
        {
            ["channel_id"] = "mm_updates",
            ["icon"] = "ic_stat_notify",
            ["color"] = "#B607BC",
            ["click_action"] = "FLUTTER_NOTIFICATION_CLICK",
        };
        if (!string.IsNullOrEmpty(tag)) androidNotification["tag"] = tag;

        var payload = new JsonObject
        {
            ["message"] = new JsonObject
            {
                ["token"] = token,
                ["notification"] = new JsonObject { ["title"] = title, ["body"] = body },
                ["data"] = dataNode,
                ["android"] = new JsonObject { ["priority"] = "HIGH", ["notification"] = androidNotification },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_account.ProjectId}/messages:send")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode) return PushResult.Sent;

        var text = await res.Content.ReadAsStringAsync(ct);
        // الجهاز حذف التطبيق أو الرمز لم يعد صالحاً
        if (res.StatusCode == HttpStatusCode.NotFound || text.Contains("UNREGISTERED") ||
            (res.StatusCode == HttpStatusCode.BadRequest && text.Contains("registration token", StringComparison.OrdinalIgnoreCase)))
            return PushResult.InvalidToken;
        if (res.StatusCode == HttpStatusCode.Unauthorized) _accessToken = null;
        _logger.LogWarning("تعذّر إرسال إشعار Firebase ({Status}): {Body}", (int)res.StatusCode, text.Length > 300 ? text[..300] : text);
        return PushResult.Failed;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTime.UtcNow < _accessTokenExpires) return _accessToken;

        var assertion = CreateJwt(_account, DateTimeOffset.UtcNow);
        using var res = await _http.PostAsync(_account.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion,
        }), ct);
        var json = await res.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
        if (!res.IsSuccessStatusCode || json?["access_token"] is null)
            throw new InvalidOperationException("رفضت Google بيانات حساب خدمة Firebase. تأكد من ملف حساب الخدمة.");
        _accessToken = json["access_token"]!.GetValue<string>();
        var seconds = json["expires_in"]?.GetValue<int>() ?? 3600;
        _accessTokenExpires = DateTime.UtcNow.AddSeconds(seconds - 120);
        return _accessToken;
    }

    internal static string CreateJwt(FirebaseServiceAccount account, DateTimeOffset now)
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = B64(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = account.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey.Replace("\\n", "\n"));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{B64(signature)}";
    }
}

/// <summary>
/// يرسل إشعارات العملاء الجديدة إلى هواتفهم فور إنشائها (كل بضع ثوانٍ).
/// يعمل فقط عند ضبط حساب خدمة Firebase؛ وإلا يبقى التطبيق على الفحص الدوري.
/// </summary>
public class PushDispatchService : BackgroundService
{
    /// <summary>لا نرسل للهاتف إشعارات أقدم من هذا (حتى لا تنهال الإشعارات القديمة عند أول تفعيل).</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly FcmSender _sender;
    private readonly ILogger<PushDispatchService> _logger;

    public PushDispatchService(IServiceScopeFactory scopes, FcmSender sender, ILogger<PushDispatchService> logger)
    {
        _scopes = scopes;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("إشعارات الهاتف عبر Firebase مفعّلة (المشروع: {Project}).", _sender.ProjectId);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await DispatchOnceAsync(db, _sender, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذّرت دورة إرسال إشعارات الهاتف.");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ContinueWith(_ => { });
        }
    }

    /// <summary>دورة واحدة: يرسل المعلّق ويعلّمه كمُرسل، ويحذف رموز الأجهزة غير الصالحة. يرجع عدد الإشعارات المعالجة.</summary>
    public static async Task<int> DispatchOnceAsync(AppDbContext db, FcmSender sender, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - MaxAge;
        var pending = await db.Notifications
            .Where(n => n.RecipientType == Notification.ToCustomer && n.PushSentAt == null && n.CreatedAt > cutoff)
            .OrderBy(n => n.CreatedAt)
            .Take(300)
            .ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var customerIds = pending.Select(n => n.RecipientId).Distinct().ToList();
        var tokens = await db.DeviceTokens.Where(d => customerIds.Contains(d.CustomerId)).ToListAsync(ct);
        var byCustomer = tokens.GroupBy(t => t.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
        var invalid = new HashSet<DeviceToken>();

        foreach (var n in pending)
        {
            if (byCustomer.TryGetValue(n.RecipientId, out var devices))
            {
                var data = new Dictionary<string, string> { ["id"] = n.Id.ToString(), ["type"] = n.Type };
                if (!string.IsNullOrEmpty(n.Link)) data["link"] = n.Link;
                foreach (var d in devices.Where(d => !invalid.Contains(d)))
                {
                    var result = await sender.SendAsync(d.Token, n.Title, n.Body, data, tag: n.Id.ToString(), ct);
                    if (result == PushResult.InvalidToken) invalid.Add(d);
                }
            }
            n.PushSentAt = DateTime.UtcNow;
        }

        if (invalid.Count > 0) db.DeviceTokens.RemoveRange(invalid);
        await db.SaveChangesAsync(ct);
        return pending.Count;
    }
}
