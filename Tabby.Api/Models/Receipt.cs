namespace Tabby.Api.Models
{
    public class Receipt
    {
        public int Id { get; set; }

        // e.g. "Groceries", "Pizza night"
        public string Title { get; set; } = string.Empty;

        // Total bill. decimal (not double) because it's money.
        public decimal TotalAmount { get; set; }

        // The user who paid the bill and created the receipt
        public int PaidById { get; set; }
        public User PaidBy { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Who owes what on this receipt
        public List<ReceiptShare> Shares { get; set; } = new();

        // The per-receipt chat
        public List<ReceiptMessage> Messages { get; set; } = new();
    }
}
