using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Models;
using Tabby.Api.Hubs;
using Microsoft.AspNetCore.SignalR;


namespace Tabby.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/friends")]
    public class FriendsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hub;

        public FriendsController(AppDbContext db, IHubContext<ChatHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        // The logged-in user's id, read from the JWT's "sub" claim
        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")!);

        // Tells the other person's open tabs that something changed in their friends
        private async Task PushAsync(int toUserId, string kind)
        {
            var me = await _db.Users.FindAsync(CurrentUserId);
            if (me is null) return;

            await _hub.Clients
                .Group(ChatHub.UserGroupName(toUserId))
                .SendAsync("FriendsChanged", new { kind, user = ToDto(me) });
        }

        // GET api/friends -> friends + incoming + outgoing requests
        [HttpGet]
        public async Task<ActionResult<FriendsOverviewDto>> GetOverview()
        {
            var me = CurrentUserId;

            var rows = await _db.Friendships
                .Include(f => f.Requester)
                .Include(f => f.Addressee)
                .Where(f => f.RequesterId == me || f.AddresseeId == me)
                .ToListAsync();

            var overview = new FriendsOverviewDto();

            foreach (var f in rows)
            {
                if (f.Status == FriendshipStatus.Accepted)
                {
                    // The friend is whichever side isn't me
                    var other = f.RequesterId == me ? f.Addressee : f.Requester;
                    var dto = ToDto(other);
                    dto.FriendshipId = f.Id;
                    overview.Friends.Add(dto);
                }
                else if (f.AddresseeId == me)
                {
                    overview.Incoming.Add(ToRequestDto(f, f.Requester));
                }
                else
                {
                    overview.Outgoing.Add(ToRequestDto(f, f.Addressee));
                }
            }

            return overview;
        }

        // POST api/friends/requests  { "fullTag": "stancho#4821" }
        [HttpPost("requests")]
        public async Task<IActionResult> SendRequest(SendFriendRequestRequest request)
        {
            var me = CurrentUserId;

            // Split "stancho#4821" at the last '#'
            var input = request.FullTag.Trim();
            var hashIndex = input.LastIndexOf('#');
            if (hashIndex <= 0 || hashIndex == input.Length - 1)
                return BadRequest(new { message = "Use the format username#1234." });

            var username = input[..hashIndex];
            var tag = input[(hashIndex + 1)..];

            var target = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Tag == tag);

            if (target is null)
                return NotFound(new { message = "No user with that tag." });

            if (target.Id == me)
                return BadRequest(new { message = "You can't add yourself." });

            // Check both directions, since the unique index only covers one
            var existing = await _db.Friendships.FirstOrDefaultAsync(f =>
                (f.RequesterId == me && f.AddresseeId == target.Id) ||
                (f.RequesterId == target.Id && f.AddresseeId == me));

            if (existing is not null)
            {
                if (existing.Status == FriendshipStatus.Accepted)
                    return Conflict(new { message = "You're already friends." });

                // They already asked me: sending a request back just accepts theirs
                if (existing.AddresseeId == me)
                {
                    existing.Status = FriendshipStatus.Accepted;
                    await _db.SaveChangesAsync();
                    await PushAsync(target.Id, "RequestAccepted");
                    return Ok(new { message = "Friend request accepted." });
                }

                return Conflict(new { message = "Request already sent." });
            }

            _db.Friendships.Add(new Friendship
            {
                RequesterId = me,
                AddresseeId = target.Id
            });
            await _db.SaveChangesAsync();
            await PushAsync(target.Id, "RequestReceived");

            return Ok(new { message = "Friend request sent." });
        }

        // POST api/friends/requests/5/accept
        [HttpPost("requests/{id}/accept")]
        public async Task<IActionResult> Accept(int id)
        {
            var friendship = await _db.Friendships.FindAsync(id);

            // Only the person who received the request can accept it
            if (friendship is null || friendship.AddresseeId != CurrentUserId)
                return NotFound();

            if (friendship.Status == FriendshipStatus.Accepted)
                return Conflict(new { message = "Already accepted." });

            friendship.Status = FriendshipStatus.Accepted;
            await _db.SaveChangesAsync();
            await PushAsync(friendship.RequesterId, "RequestAccepted");

            return Ok(new { message = "Friend request accepted." });
        }

        // DELETE api/friends/5 -> decline, cancel, or unfriend
        [HttpDelete("{id}")]
        public async Task<IActionResult> Remove(int id)
        {
            var me = CurrentUserId;
            var friendship = await _db.Friendships.FindAsync(id);

            // Either person in the relationship may remove it
            if (friendship is null ||
                (friendship.RequesterId != me && friendship.AddresseeId != me))
                return NotFound();

            var otherId = friendship.RequesterId == me ? friendship.AddresseeId : friendship.RequesterId;

            _db.Friendships.Remove(friendship);
            await _db.SaveChangesAsync();
            await PushAsync(otherId, "Removed");

            return NoContent();
        }

        private static FriendDto ToDto(User u) => new()
        {
            UserId = u.Id,
            Username = u.Username,
            Tag = u.Tag,
            FullTag = u.FullTag
        };

        private static FriendRequestDto ToRequestDto(Friendship f, User other) => new()
        {
            FriendshipId = f.Id,
            User = ToDto(other),
            CreatedAt = f.CreatedAt
        };
    }
}
