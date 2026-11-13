namespace Tabby.Api.DTOs
{
    public class UserDtos
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string FullTag { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
