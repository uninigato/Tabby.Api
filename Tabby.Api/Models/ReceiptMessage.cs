namespace Tabby.Api.Models
{
    public class ReceiptMessage
    {
        public int Id { get; set; }

        public int ReceiptId { get; set; }
        public Receipt Receipt { get; set; } = null!;

        // Who wrote the message
        public int SenderId { get; set; }
        public User Sender { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
