namespace Tabby.Api.Models;

public class DirectMessage
{
    public int Id { get; set; }

    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public int RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}