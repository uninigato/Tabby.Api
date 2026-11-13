using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;

namespace Tabby.Api.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;

        public ChatHub(AppDbContext db)
        {
            _db = db;
        }

        // One SignalR "group" per receipt, so a message only reaches people on that receipt
        public static string GroupName(int receiptId) => $"receipt-{receiptId}";

        private int CurrentUserId =>
            int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Context.User.FindFirstValue("sub")!);

        // One private group per user, so we can push to "everyone logged in as user 7"
        // (a person can have several tabs or devices open, and all of them are in it)
        public static string UserGroupName(int userId) => $"user-{userId}";

        // Runs automatically every time a client connects
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(CurrentUserId));
            await base.OnConnectedAsync();
        }

        // The React chat panel calls this when a receipt's chat is opened
        public async Task JoinReceipt(int receiptId)
        {
            var me = CurrentUserId;

            var isParticipant = await _db.Receipts.AnyAsync(r => r.Id == receiptId &&
                (r.PaidById == me || r.Shares.Any(s => s.UserId == me)));

            if (!isParticipant)
                throw new HubException("Receipt not found.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(receiptId));
        }

        // ...and this when it's closed
        public Task LeaveReceipt(int receiptId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(receiptId));
    }
}
