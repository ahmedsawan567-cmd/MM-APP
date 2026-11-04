using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MMApplication.Api.Controllers;

/// <summary>فحص اتصال عام يستخدمه التطبيق للتأكد من عنوان الخادم قبل تسجيل الدخول.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "almaraa-mall", time = DateTime.UtcNow });
}
