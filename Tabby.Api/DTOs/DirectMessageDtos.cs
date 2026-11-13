using System.ComponentModel.DataAnnotations;

namespace Tabby.Api.DTOs;

// What the client sends when writing to a friend
public class SendDirectMessageRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

// One chat bubble in a conversation
public class DirectMessageDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int RecipientId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class UnreadMessagesDto
{
    public int FriendId { get; set; }
    public int Count { get; set; }
}