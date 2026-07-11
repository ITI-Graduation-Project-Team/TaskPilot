using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TaskPilot.DTOs;
using MailKit.Net.Smtp;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Services.Interfaces.External;
using TaskPilot.Infrastructure.Settings;


namespace TaskPilot.Infrastructure.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailsettings;
        public EmailService(IOptions<EmailSettings> options)
        {
            _emailsettings = options.Value;
        }
        public async Task<Result> SendEmailAsync(EmailRequest emailRequest)
        {
            try
            {
                var email = new MimeMessage
                {
                    Sender = MailboxAddress.Parse(_emailsettings.Email),
                    Subject = emailRequest.Subject,
                };
                email.To.Add(MailboxAddress.Parse(emailRequest.To));
                var builder = new BodyBuilder();
                builder.HtmlBody = emailRequest.Body;
                email.Body = builder.ToMessageBody();
                email.From.Add(new MailboxAddress(_emailsettings.DisplayName, _emailsettings.Email));
                using var smtp = new SmtpClient();
                smtp.Connect(_emailsettings.Host, _emailsettings.Port, SecureSocketOptions.StartTls);

                smtp.Authenticate(_emailsettings.Email, _emailsettings.Password);
                await smtp.SendAsync(email);
                smtp.Disconnect(true);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[EmailService Error]: Failed to send email to {emailRequest.To}. Details: {ex.Message}");
                Console.ResetColor();
                return Result.Failure(new Error("Email.SendFailed", ErrorType.Failure, ex.Message));
            }
        }

       
    }
}
