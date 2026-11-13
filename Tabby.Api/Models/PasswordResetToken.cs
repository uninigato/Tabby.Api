namespace Tabby.Api.Models
{
    public class PasswordResetToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // SHA-256 hash of the token we emailed. The raw token is never stored.
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        // Set when the token is used, so a link works only once
        public DateTime? UsedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
