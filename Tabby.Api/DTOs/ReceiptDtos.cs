using System.ComponentModel.DataAnnotations;

namespace Tabby.Api.DTOs;

// One person on a new receipt. Leave Amount empty for an equal split.
public class ShareInput
{
    [Required]
    public int UserId { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal? Amount { get; set; }
}

// What the create-receipt modal sends
public class CreateReceiptRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal TotalAmount { get; set; }

    // The other people on the bill (the payer is added automatically)
    [Required, MinLength(1)]
    public List<ShareInput> Shares { get; set; } = new();

    // false = the people on the receipt split the whole total between them
    public bool IncludeMyShare { get; set; } = true;
}

// One person's portion, as returned to the client
public class ShareDto
{
    public int ShareId { get; set; }
    public FriendDto User { get; set; } = new();
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
}

// A receipt card, with everything the UI needs to draw it
public class ReceiptDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public FriendDto PaidBy { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public List<ShareDto> Shares { get; set; } = new();
}

// What the paid/unpaid toggle sends
public class SetPaidRequest
{
    public bool IsPaid { get; set; }
}