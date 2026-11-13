using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Hubs;
using Tabby.Api.Models;

namespace Tabby.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/messages")]
public class DirectMessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public DirectMessagesController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")!);

    // You can only talk to accepted friends
    private Task<bool> AreFriendsAsync(int me, int other) =>
        _db.Friendships.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == me && f.AddresseeId == other) ||
             (f.RequesterId == other && f.AddresseeId == me)));

    // GET api/messages/7            -> the last 100 messages with friend 7, oldest first
    // GET api/messages/7?afterId=42 -> only messages newer than id 42

    [HttpGet("{friendId:int}")]
    public async Task<ActionResult<List<DirectMessageDto>>> GetConversation(
        int friendId, [FromQuery] int? afterId)
    {
        var me = CurrentUserId;

        if (!await AreFriendsAsync(me, friendId))
            return NotFound();

        var query = _db.DirectMessages.Where(m =>
            (m.SenderId == me && m.RecipientId == friendId) ||
            (m.SenderId == friendId && m.RecipientId == me));

        List<DirectMessage> messages;

        if (afterId.HasValue)
        {
            messages = await query
                .Where(m => m.Id > afterId.Value)
                .OrderBy(m => m.Id)
                .ToListAsync();
        }
        else
        {
            // Newest 100, then flipped back into reading order
            messages = await query
                .OrderByDescending(m => m.Id)
                .Take(100)
                .ToListAsync();
            messages.Reverse();
        }

        return messages.Select(ToDto).ToList();
    }

    // POST api/messages/7   { "content": "hey" }

    [HttpPost("{friendId:int}")]
    public async Task<ActionResult<DirectMessageDto>> Send(
        int friendId, SendDirectMessageRequest request)
    {
        var me = CurrentUserId;

        if (!await AreFriendsAsync(me, friendId))
            return NotFound();

        // [Required] lets "   " through, so check after trimming
        var content = request.Content.Trim();
        if (content.Length == 0)
            return BadRequest(new { message = "Message can't be empty." });

        var message = new DirectMessage
        {
            SenderId = me,
            RecipientId = friendId,
            Content = content
        };

        _db.DirectMessages.Add(message);
        await _db.SaveChangesAsync();

        var dto = ToDto(message);

        // Push to every open tab of both people (the sender's other tabs too)
        await _hub.Clients
            .Groups(ChatHub.UserGroupName(friendId), ChatHub.UserGroupName(me))
            .SendAsync("ReceiveDirectMessage", dto);

        return Ok(dto);
    }

    // GET api/messages/unread -> [{ friendId: 7, count: 3 }, ...]
    [HttpGet("unread")]
    public async Task<ActionResult<List<UnreadMessagesDto>>> GetUnread()
    {
        var me = CurrentUserId;

        var counts = await _db.DirectMessages
            .Where(m => m.RecipientId == me && m.ReadAt == null)
            .GroupBy(m => m.SenderId)
            .Select(g => new UnreadMessagesDto { FriendId = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts;
    }

    // POST api/messages/7/read -> everything friend 7 sent me counts as read
    [HttpPost("{friendId:int}/read")]
    public async Task<IActionResult> MarkRead(int friendId)
    {
        var me = CurrentUserId;

        // One UPDATE statement, no need to load the rows first
        await _db.DirectMessages
            .Where(m => m.SenderId == friendId && m.RecipientId == me && m.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, (DateTime?)DateTime.UtcNow));

        return NoContent();
    }

    private static DirectMessageDto ToDto(DirectMessage m) => new()
    {
        Id = m.Id,
        SenderId = m.SenderId,
        RecipientId = m.RecipientId,
        Content = m.Content,
        SentAt = m.SentAt
    };
}