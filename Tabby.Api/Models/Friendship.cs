namespace Tabby.Api.Models
{
    public enum FriendshipStatus
    {
        Pending = 0,
        Accepted = 1
    }

    public class Friendship
    {
        public int Id { get; set; }

        // The user who sent the friend request
        public int RequesterId { get; set; }
        public User Requester { get; set; } = null!;

        // The user who received it
        public int AddresseeId { get; set; }
        public User Addressee { get; set; } = null!;

        public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
