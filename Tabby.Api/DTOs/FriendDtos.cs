using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tabby.Api.DTOs
{
    // What the client sends to add a friend
    public class SendFriendRequestRequest
    {
        [Required]
        public string FullTag { get; set; } = string.Empty;

    }

    // A person as shown in the friends list
    public class FriendDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string FullTag { get; set; } = string.Empty;

        // Only filled in the friends list, where it's needed to unfriend someone
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? FriendshipId { get; set; }
    }

    // A pending request, incoming or outgoing
    public class FriendRequestDto
    {

        public int FriendshipId { get; set; }
        public FriendDto User { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    // Everything the Friends tab needs in one response
    public class FriendsOverviewDto
    {
        public List<FriendDto> Friends { get; set; } = new();
        public List<FriendRequestDto> Incoming { get; set; } = new();
        public List<FriendRequestDto> Outgoing { get; set; } = new();
    }
}
