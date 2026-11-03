using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MMApplication.Application.Interfaces;
using MMApplication.Infrastructure.Data;
using MMApplication.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers With Views (ASP.NET Core MVC .NET 9) & JSON serialization
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Configure Unicode HtmlEncoder for seamless Arabic rendering without entity encoding
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(allowedRanges: new[] { UnicodeRanges.BasicLatin, UnicodeRanges.Arabic, UnicodeRanges.ArabicPresentationFormsA, UnicodeRanges.ArabicPresentationFormsB, UnicodeRanges.ArabicSupplement }));

// 2. Database Context (Supabase Cloud Schema & Resilient Engine)
var inMemoryDbName = builder.Configuration.GetValue<string>("InMemoryDbName") ?? "MMAppDb_Supabase";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseInMemoryDatabase(inMemoryDbName);
    options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
});

// 3. Dependency Injection (Services)
var ocrProvider = builder.Configuration.GetValue<string>("OcrProvider") ?? "Mock";
switch (ocrProvider.ToLowerInvariant())
{
    case "azure":
        builder.Services.AddScoped<IReceiptOcrService, AzureDocumentIntelligenceOcrService>();
        break;
    case "openai":
        builder.Services.AddScoped<IReceiptOcrService, OpenAiVisionOcrService>();
        break;
    default:
        builder.Services.AddScoped<IReceiptOcrService, MockReceiptOcrService>();
        break;
}

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IFlexibleLoyaltyEvaluationService, FlexibleLoyaltyEvaluationService>();

// 4. Authentication (Cookies for Web MVC Dashboard & JWT Bearer for Mobile API)
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "MM_Loyalty_Super_Secure_Secret_Key_2026_For_Development_And_Production_Tokens_Must_Be_Long!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "MMAppApi";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "MMAppClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JWT_OR_COOKIE";
    options.DefaultChallengeScheme = "JWT_OR_COOKIE";
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "MMApp_Auth_Session";
    options.LoginPath = "/AdminDashboard/Login";
    options.LogoutPath = "/AdminDashboard/Logout";
    options.AccessDeniedPath = "/AdminDashboard/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddPolicyScheme("JWT_OR_COOKIE", "JWT_OR_COOKIE", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
});

builder.Services.AddAuthorization();

// 5. OpenAPI / Swagger Documentation with JWT Security Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "M M Application - Loyalty API (نظام ولاء المتاجر والمول)",
        Version = "v1",
        Description = "واجهة برمجة التطبيقات لمنظومة ولاء المتاجر والمول (MM Application) - مصادقة JWT، رفع الفواتير، والتعرف الذكي واحتساب النقاط.",
        Contact = new OpenApiContact
        {
            Name = "MM Loyalty Team",
            Email = "support@mmapp.com"
        }
    });

    // JWT Bearer Configuration in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل رمز الـ JWT Token هنا. مثال: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Support XML documentation comments if file exists
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 6. Automatic Database Migration & Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasherService>();
        logger.LogInformation("Applying database schema and seed data...");
        if (db.Database.IsRelational())
        {
            await DatabaseSchemaAligner.EnsureSchemaAlignedAsync(db, logger);
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
        await DataSeeder.SeedAsync(db, hasher);
        logger.LogInformation("Database is ready and seeded with test accounts.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Could not automatically initialize database: {Message}", ex.Message);
    }
}

// 7. HTTP Pipeline configuration
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MM Loyalty API v1");
        c.RoutePrefix = "swagger"; // Serve Swagger at /swagger
    });
}

// Serve static files for styles, scripts, and uploaded receipt images
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{action=Index}/{id?}",
    defaults: new { controller = "AdminDashboard" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AdminDashboard}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

// Required for Integration Testing with WebApplicationFactory
public partial class Program { }
