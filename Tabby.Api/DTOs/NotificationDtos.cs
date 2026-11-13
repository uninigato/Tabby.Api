namespace Tabby.Api.DTOs;

// One item in the notifications list
public class NotificationDto
{
    public int Id { get; set; }

    // "SharePaid" for now. A string, so the frontend can switch on it easily.
    public string Type { get; set; } = string.Empty;

    // Who did it (the person who paid)
    public FriendDto Actor { get; set; } = new();

    // Which receipt it's about, so the UI can link to it
    public int ReceiptId { get; set; }
    public string ReceiptTitle { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// For the unread badge on the nav rail
public class UnreadCountDto
{
    public int Count { get; set; }
}