using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Tabby.Api.Hubs;

namespace Tabby.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/receipts/{receiptId}/messages")]
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hub;

        public MessagesController(AppDbContext db, IHubContext<ChatHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")!);

        // Only the payer and the people with a share may use this receipt's chat
        private Task<bool> IsParticipantAsync(int receiptId, int userId) =>
            _db.Receipts.AnyAsync(r => r.Id == receiptId &&
                (r.PaidById == userId || r.Shares.Any(s => s.UserId == userId)));

        // GET api/receipts/5/messages            -> the whole chat, oldest first
        // GET api/receipts/5/messages?afterId=42 -> only messages newer than id 42
        [HttpGet]
        public async Task<ActionResult<List<MessageDto>>> GetMessages(
            int receiptId, [FromQuery] int? afterId)
        {
            if (!await IsParticipantAsync(receiptId, CurrentUserId))
                return NotFound();

            var query = _db.ReceiptMessages
                .Include(m => m.Sender)
                .Where(m => m.ReceiptId == receiptId);

            if (afterId.HasValue)
                query = query.Where(m => m.Id > afterId.Value);

            var messages = await query
                .OrderBy(m => m.SentAt)
                .ThenBy(m => m.Id)
                .ToListAsync();

            return messages.Select(ToDto).ToList();
        }

        // POST api/receipts/5/messages   { "content": "I'll pay you back Friday" }
        [HttpPost]
        public async Task<ActionResult<MessageDto>> Send(int receiptId, SendMessageRequest request)
        {
            var me = CurrentUserId;

            if (!await IsParticipantAsync(receiptId, me))
                return NotFound();

            // [Required] lets "   " through, so check after trimming
            var content = request.Content.Trim();
            if (content.Length == 0)
                return BadRequest(new { message = "Message can't be empty." });

            var message = new ReceiptMessage
            {
                ReceiptId = receiptId,
                SenderId = me,
                Content = content
            };

            _db.ReceiptMessages.Add(message);
            await _db.SaveChangesAsync();

            // Load the sender so the response can include their name and tag
            await _db.Entry(message).Reference(m => m.Sender).LoadAsync();

            var dto = ToDto(message);

            // Push the new message to everyone who has this receipt's chat open
            await _hub.Clients
                .Group(ChatHub.GroupName(receiptId))
                .SendAsync("ReceiveMessage", dto);

            return Ok(dto);
        }

        private static MessageDto ToDto(ReceiptMessage m) => new()
        {
            Id = m.Id,
            ReceiptId = m.ReceiptId,
            Sender = new FriendDto
            {
                UserId = m.Sender.Id,
                Username = m.Sender.Username,
                Tag = m.Sender.Tag,
                FullTag = m.Sender.FullTag
            },
            Content = m.Content,
            SentAt = m.SentAt
        };
    }
}
