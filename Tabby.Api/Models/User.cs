namespace Tabby.Api.Models
{
    public class User
    {
        public int Id { get; set; }

        // The name the person picks, e.g. "stancho"
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // 4-digit discriminator, e.g. "1234" -> shown as stancho#1234
        public string Tag { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Convenience property, not stored in the database
        public string FullTag => $"{Username}#{Tag}";

        
    }
}
