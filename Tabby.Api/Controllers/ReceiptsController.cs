using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Tabby.Api.Hubs;
using Tabby.Api.Services;

namespace Tabby.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/receipts")]
    public class ReceiptsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hub;

        public ReceiptsController(AppDbContext db, IHubContext<ChatHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")!);

        // Receipts always load together with the payer and each share's user
        private IQueryable<Receipt> WithDetails() => _db.Receipts
            .Include(r => r.PaidBy)
            .Include(r => r.Shares).ThenInclude(s => s.User);

        // GET api/receipts -> every receipt I paid or owe a share on
        [HttpGet]
        public async Task<ActionResult<List<ReceiptDto>>> GetMine()
        {
            var me = CurrentUserId;

            var receipts = await WithDetails()
                .Where(r => r.PaidById == me || r.Shares.Any(s => s.UserId == me))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return receipts.Select(ToDto).ToList();
        }

        // GET api/receipts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ReceiptDto>> GetById(int id)
        {
            var me = CurrentUserId;
            var receipt = await WithDetails().FirstOrDefaultAsync(r => r.Id == id);

            if (receipt is null || !IsParticipant(receipt, me))
                return NotFound();

            return ToDto(receipt);
        }

        // POST api/receipts
        [HttpPost]
        public async Task<ActionResult<ReceiptDto>> Create(CreateReceiptRequest request)
        {
            var me = CurrentUserId;
            var otherIds = request.Shares.Select(s => s.UserId).ToList();

            if (otherIds.Contains(me))
                return BadRequest(new { message = "You're added automatically, don't include yourself." });

            if (otherIds.Distinct().Count() != otherIds.Count)
                return BadRequest(new { message = "Each person can only appear once." });

            // You can only split with accepted friends
            var friendIds = await _db.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                            (f.RequesterId == me || f.AddresseeId == me))
                .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            if (otherIds.Any(id => !friendIds.Contains(id)))
                return BadRequest(new { message = "You can only split a receipt with your friends." });

            // Either everyone has an amount (custom split) or nobody does (equal split)
            var withAmount = request.Shares.Count(s => s.Amount.HasValue);
            if (withAmount != 0 && withAmount != request.Shares.Count)
                return BadRequest(new { message = "Set an amount for everyone, or for no one." });

            var total = Math.Round(request.TotalAmount, 2, MidpointRounding.AwayFromZero);
            var receipt = new Receipt
            {
                Title = request.Title.Trim(),
                TotalAmount = total,
                PaidById = me
            };

            if (withAmount == 0)
            {
                // Equal split. If my own share is included, I take one portion too (last).
                var people = otherIds.Count + (request.IncludeMyShare ? 1 : 0);
                var amounts = SplitCalculator.EqualSplit(total, people);

                for (var i = 0; i < otherIds.Count; i++)
                    AddShare(receipt, otherIds[i], amounts[i], isPaid: false);

                if (request.IncludeMyShare)
                    AddShare(receipt, me, amounts[otherIds.Count], isPaid: true);
            }
            else
            {
                // Custom split
                var othersSum = 0m;
                foreach (var s in request.Shares)
                {
                    var amount = Math.Round(s.Amount!.Value, 2);
                    othersSum += amount;
                    AddShare(receipt, s.UserId, amount, isPaid: false);
                }

                if (othersSum > total)
                    return BadRequest(new { message = "The shares add up to more than the total." });

                if (request.IncludeMyShare)
                {
                    // I cover whatever the others don't
                    var myPortion = total - othersSum;
                    if (myPortion > 0)
                        AddShare(receipt, me, myPortion, isPaid: true);
                }
                else if (othersSum != total)
                {
                    return BadRequest(new { message = "Without your own share, the amounts must add up to the total." });
                }
            }

            _db.Receipts.Add(receipt);

            // Everyone I put on this receipt gets a notification (not me)
            var notifications = otherIds
                .Select(id => new Notification
                {
                    RecipientId = id,
                    ActorId = me,
                    Receipt = receipt,
                    Type = NotificationType.AddedToReceipt
                })
                .ToList();
            _db.Notifications.AddRange(notifications);

            await _db.SaveChangesAsync();

            var created = await WithDetails().FirstAsync(r => r.Id == receipt.Id);
            await PushReceiptAsync(created);

            // Push the notifications too. The DTO needs the actor, and the receipt is already attached.
            foreach (var n in notifications)
            {
                n.Actor = created.PaidBy;
                await _hub.Clients
                    .Group(ChatHub.UserGroupName(n.RecipientId))
                    .SendAsync("ReceiveNotification", NotificationsController.ToDto(n));
            }

            return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, ToDto(created));
        }

        // PATCH api/receipts/5/shares/12/paid   { "isPaid": true }
        [HttpPatch("{id}/shares/{shareId}/paid")]
        public async Task<ActionResult<ReceiptDto>> SetPaid(int id, int shareId, SetPaidRequest request)
        {
            var me = CurrentUserId;
            var receipt = await WithDetails().FirstOrDefaultAsync(r => r.Id == id);

            if (receipt is null || !IsParticipant(receipt, me))
                return NotFound();

            var share = receipt.Shares.FirstOrDefault(s => s.Id == shareId);
            if (share is null)
                return NotFound();

            // You can change your own share, and the payer can change anyone's
            var isPayer = receipt.PaidById == me;
            if (!isPayer && share.UserId != me)
                return Forbid();

            var wasPaid = share.IsPaid;
            share.IsPaid = request.IsPaid;
            share.PaidAt = request.IsPaid ? DateTime.UtcNow : null;

            // Tell the payer when someone marks their own share as paid.
            // Only on a real change, so repeated clicks don't spam notifications.
            Notification? notification = null;

            if (request.IsPaid && !wasPaid && share.UserId == me && !isPayer)
            {
                notification = new Notification
                {
                    RecipientId = receipt.PaidById,
                    ActorId = me,
                    ReceiptId = receipt.Id,
                    Type = NotificationType.SharePaid,
                    // Already loaded, so we can build the DTO without another query
                    Actor = share.User,
                    Receipt = receipt
                };
                _db.Notifications.Add(notification);
            }

            await _db.SaveChangesAsync();

            // Push it to the payer's open tabs and devices
            if (notification is not null)
            {
                await _hub.Clients
                    .Group(ChatHub.UserGroupName(receipt.PaidById))
                    .SendAsync("ReceiveNotification", NotificationsController.ToDto(notification));
            }

            await _db.SaveChangesAsync();
            await PushReceiptAsync(receipt);

            return ToDto(receipt);
        }

        // DELETE api/receipts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var me = CurrentUserId;
            var receipt = await _db.Receipts
                .Include(r => r.Shares)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt is null || receipt.PaidById != me)
                return NotFound();

            var groups = GroupsFor(receipt); // work out who to tell before the rows are gone

            // Shares, messages and notifications are removed too (cascade)
            _db.Receipts.Remove(receipt);
            await _db.SaveChangesAsync();

            await _hub.Clients.Groups(groups).SendAsync("ReceiptDeleted", new { id });

            return NoContent();
        }

        // Everyone who sees this receipt: the payer and each person with a share
        private static List<string> GroupsFor(Receipt r) =>
            r.Shares.Select(s => s.UserId)
                .Append(r.PaidById)
                .Distinct()
                .Select(ChatHub.UserGroupName)
                .ToList();

        // Tell everyone on the receipt (my own other tabs too) to show this version
        private Task PushReceiptAsync(Receipt r) =>
            _hub.Clients.Groups(GroupsFor(r)).SendAsync("ReceiptChanged", ToDto(r));

        private static bool IsParticipant(Receipt r, int userId) =>
            r.PaidById == userId || r.Shares.Any(s => s.UserId == userId);

        private static void AddShare(Receipt receipt, int userId, decimal amount, bool isPaid)
        {
            receipt.Shares.Add(new ReceiptShare
            {
                UserId = userId,
                Amount = amount,
                IsPaid = isPaid,
                PaidAt = isPaid ? DateTime.UtcNow : null
            });
        }

        private static FriendDto ToFriend(User u) => new()
        {
            UserId = u.Id,
            Username = u.Username,
            Tag = u.Tag,
            FullTag = u.FullTag
        };

        private static ReceiptDto ToDto(Receipt r) => new()
        {
            Id = r.Id,
            Title = r.Title,
            TotalAmount = r.TotalAmount,
            PaidBy = ToFriend(r.PaidBy),
            CreatedAt = r.CreatedAt,
            Shares = r.Shares.Select(s => new ShareDto
            {
                ShareId = s.Id,
                User = ToFriend(s.User),
                Amount = s.Amount,
                IsPaid = s.IsPaid,
                PaidAt = s.PaidAt
            }).ToList()
        };
    }
}
