using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;
using MMApplication.Infrastructure.Data;

namespace MMApplication.Api.Controllers;

/// <summary>إشعارات العميل في التطبيق.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context) => _context = context;

    private string? CustomerId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

    private IQueryable<Notification> Mine() =>
        _context.Notifications.Where(n => n.RecipientType == Notification.ToCustomer && n.RecipientId == CustomerId);

    /// <summary>آخر الإشعارات (الأحدث أولاً). since: لجلب الجديد فقط منذ وقت معيّن.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? since, [FromQuery] int take = 50)
    {
        if (CustomerId == null) return Unauthorized();
        var query = Mine().AsNoTracking();
        if (since.HasValue) query = query.Where(n => n.CreatedAt > since.Value.ToUniversalTime());
        var items = await query.OrderByDescending(n => n.CreatedAt).Take(Math.Clamp(take, 1, 100))
            .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt })
            .ToListAsync();
        var unread = await Mine().CountAsync(n => !n.IsRead);
        return Ok(new { unread, items });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        if (CustomerId == null) return Unauthorized();
        return Ok(new { unread = await Mine().CountAsync(n => !n.IsRead) });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var n = await Mine().FirstOrDefaultAsync(x => x.Id == id);
        if (n == null) return NotFound();
        if (!n.IsRead) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; await _context.SaveChangesAsync(); }
        return Ok(new { success = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (CustomerId == null) return Unauthorized();
        var unread = await Mine().Where(n => !n.IsRead).ToListAsync();
        foreach (var n in unread) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        await _context.SaveChangesAsync();
        return Ok(new { success = true, count = unread.Count });
    }
}
