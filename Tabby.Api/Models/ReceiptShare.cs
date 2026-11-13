namespace Tabby.Api.Models
{
    public class ReceiptShare
    {
        public int Id { get; set; }

        public int ReceiptId { get; set; }
        public Receipt Receipt { get; set; } = null!;

        // The person who owes this portion
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // How much this person owes
        public decimal Amount { get; set; }

        // The paid/unpaid toggle on the receipt card
        public bool IsPaid { get; set; }

        // When they marked it paid (null while unpaid)
        public DateTime? PaidAt { get; set; }
    }
}
