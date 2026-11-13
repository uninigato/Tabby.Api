using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;

namespace Tabby.Api.Services
{
    public class TagService
    {
        private readonly AppDbContext _db;

        public TagService(AppDbContext db)
        {
            _db = db;
        }

        // Returns a free 4-digit tag for this username, or null if none could be found
        public async Task<string?> GenerateUniqueTagAsync(string username)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                // 0001 - 9999, always 4 characters ("7" becomes "0007")
                var tag = Random.Shared.Next(1, 10000).ToString("D4");

                var taken = await _db.Users
                    .AnyAsync(u => u.Username == username && u.Tag == tag);

                if (!taken)
                    return tag;
            }

            return null;
        }
    }
}
