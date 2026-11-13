using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tabby.Api.Data;
using Tabby.Api.DTOs;

namespace Tabby.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")!);

        // GET api/users/me -> who am I? (used by the frontend on page load)
        [HttpGet("me")]
        public async Task<ActionResult<UserDtos>> GetMe()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);

            // The token was valid but the account is gone (e.g. the database was reset)
            if (user is null)
                return Unauthorized();

            return new UserDtos
            {
                UserId = user.Id,
                Username = user.Username,
                Tag = user.Tag,
                FullTag = user.FullTag,
                Email = user.Email
            };
        }
    }
}
