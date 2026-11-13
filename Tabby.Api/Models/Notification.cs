namespace Tabby.Api.Models;

public enum NotificationType
{
    SharePaid = 0,
    AddedToReceipt = 1
}

public class Notification
{
    public int Id { get; set; }

    // Who gets the notification
    public int RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    // Who caused it (the person who paid)
    public int ActorId { get; set; }
    public User Actor { get; set; } = null!;

    // Which receipt it's about, so the UI can link to it
    public int ReceiptId { get; set; }
    public Receipt Receipt { get; set; } = null!;

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}