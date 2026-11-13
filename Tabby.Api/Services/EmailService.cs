using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Tabby.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Sends one HTML email. Returns false (and logs) instead of throwing,
        // so a mail outage never breaks the request that triggered it.
        public async Task<bool> SendAsync(string toAddress, string subject, string htmlBody)
        {
            try
            {
                var cfg = _config.GetSection("Email");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(cfg["FromName"], cfg["FromAddress"]));
                message.To.Add(MailboxAddress.Parse(toAddress));
                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

                using var client = new SmtpClient();

                await client.ConnectAsync(
                    cfg["Host"],
                    cfg.GetValue<int>("Port"),
                    cfg.GetValue<bool>("UseSsl")
                        ? SecureSocketOptions.Auto
                        : SecureSocketOptions.None);

                // Local fake servers like smtp4dev need no login
                var username = cfg["Username"];
                if (!string.IsNullOrEmpty(username))
                    await client.AuthenticateAsync(username, cfg["Password"]);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", toAddress);
                return false;
            }
        }
    }
}
