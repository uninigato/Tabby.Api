using Microsoft.AspNetCore.Identity;
using Tabby.Api.Models;

namespace Tabby.Api.Services
{
    public class PasswordService
    {
        private readonly PasswordHasher<User> _hasher = new();

        // Turns a plain password into a salted hash we can safely store
        public string Hash(User user, string password)
            => _hasher.HashPassword(user, password);

        // Checks a login attempt against the stored hash
        public bool Verify(User user, string password)
        {
            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return result != PasswordVerificationResult.Failed;
        }
    }
}
