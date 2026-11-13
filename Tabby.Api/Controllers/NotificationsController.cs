using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Models;

namespace Tabby.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public NotificationsController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")!);

        // GET api/notifications -> my 50 most recent, newest first
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetMine()
        {
            var me = CurrentUserId;

            var notifications = await _db.Notifications
                .Include(n => n.Actor)
                .Include(n => n.Receipt)
                .Where(n => n.RecipientId == me)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return notifications.Select(ToDto).ToList();
        }

        // GET api/notifications/unread-count -> for the badge on the nav rail
        [HttpGet("unread-count")]
        public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
        {
            var me = CurrentUserId;

            var count = await _db.Notifications
                .CountAsync(n => n.RecipientId == me && !n.IsRead);

            return new UnreadCountDto { Count = count };
        }

        // POST api/notifications/12/read
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = await _db.Notifications.FindAsync(id);

            // You can only touch your own notifications
            if (notification is null || notification.RecipientId != CurrentUserId)
                return NotFound();

            notification.IsRead = true;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // POST api/notifications/read-all
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var me = CurrentUserId;

            // One UPDATE statement, no need to load the rows first
            await _db.Notifications
                .Where(n => n.RecipientId == me && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

            return NoContent();
        }

        public static NotificationDto ToDto(Notification n) => new()
        {
            Id = n.Id,
            Type = n.Type.ToString(),
            Actor = new FriendDto
            {
                UserId = n.Actor.Id,
                Username = n.Actor.Username,
                Tag = n.Actor.Tag,
                FullTag = n.Actor.FullTag
            },
            ReceiptId = n.ReceiptId,
            ReceiptTitle = n.Receipt.Title,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        };
    }
}
