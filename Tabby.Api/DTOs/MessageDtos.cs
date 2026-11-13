using System.ComponentModel.DataAnnotations;

namespace Tabby.Api.DTOs;

// What the client sends when posting a chat message
public class SendMessageRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

// A chat message as returned to the client
public class MessageDto
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public FriendDto Sender { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}