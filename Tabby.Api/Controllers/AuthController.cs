using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tabby.Api.Data;
using Tabby.Api.DTOs;
using Tabby.Api.Models;
using Tabby.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;

namespace Tabby.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PasswordService _passwords;
        private readonly JwtService _jwt;
        private readonly TagService _tags;
        private readonly EmailService _email;
        private readonly IConfiguration _config;

        public AuthController(
            AppDbContext db,
            PasswordService passwords,
            JwtService jwt,
            TagService tags,
            EmailService email,
            IConfiguration config)
        {
            _db = db;
            _passwords = passwords;
            _jwt = jwt;
            _tags = tags;
            _email = email;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var username = request.Username.Trim();

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "An account with this email already exists." });

            var tag = await _tags.GenerateUniqueTagAsync(username);
            if (tag is null)
                return Conflict(new { message = "Could not generate a tag, try a different username." });

            var user = new User
            {
                Username = username,
                Email = email,
                Tag = tag
            };
            user.PasswordHash = _passwords.Hash(user, request.Password);

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index hit: someone grabbed the same email or tag a moment ago
                return Conflict(new { message = "Account could not be created, please try again." });
            }

            return Ok(BuildResponse(user));
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Same message for "no such email" and "wrong password" on purpose
            if (user is null || !_passwords.Verify(user, request.Password))
                return Unauthorized(new { message = "Invalid email or password." });

            return Ok(BuildResponse(user));
        }

        // POST api/auth/change-password
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            // Who is asking comes from the token, never from the request body
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? User.FindFirstValue("sub")!);

            var user = await _db.Users.FindAsync(userId);
            if (user is null)
                return Unauthorized();

            if (!_passwords.Verify(user, request.CurrentPassword))
                return BadRequest(new { message = "Current password is incorrect." });

            if (request.CurrentPassword == request.NewPassword)
                return BadRequest(new { message = "New password must be different from the current one." });

            user.PasswordHash = _passwords.Hash(user, request.NewPassword);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // POST api/auth/forgot-password  { "email": "anna@test.com" }
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user is not null)
            {
                // The raw token goes in the email, only its hash goes in the database
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

                _db.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = HashToken(token),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                });
                await _db.SaveChangesAsync();

                var baseUrl = _config["Email:FrontendBaseUrl"]!.TrimEnd('/');
                var link = $"{baseUrl}/reset-password?token={token}";

                await _email.SendAsync(
                    user.Email,
                    "Reset your Tabby password",
                    $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.Username)},</p>" +
                    $"<p>Click the link below to choose a new password. It works for 30 minutes and only once.</p>" +
                    $"<p><a href=\"{link}\">Reset my password</a></p>" +
                    "<p>If you didn't ask for this, you can ignore this email.</p>");
            }

            // Same answer whether or not the email exists
            return Ok(new { message = "If that email has an account, a reset link is on its way." });
        }

        // POST api/auth/reset-password  { "token": "...", "newPassword": "..." }
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var hash = HashToken(request.Token.Trim());

            var resetToken = await _db.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            // Unknown, already used and expired all get the same answer
            if (resetToken is null || resetToken.UsedAt is not null || resetToken.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = "This reset link is invalid or has expired." });

            var user = resetToken.User;
            user.PasswordHash = _passwords.Hash(user, request.NewPassword);

            // This link is spent, and so is any other pending link for the same user
            var pending = await _db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.UsedAt == null)
                .ToListAsync();
            foreach (var t in pending)
                t.UsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
        }

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private AuthResponse BuildResponse(User user) => new()
        {
            Token = _jwt.CreateToken(user),
            UserId = user.Id,
            Username = user.Username,
            Tag = user.Tag,
            FullTag = user.FullTag,
            Email = user.Email
        };
    }
}
