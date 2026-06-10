using MailKit.Net.Smtp;
using MimeKit;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Infrastructure.Email;

public record SmtpOptions(string Host, int Port, string? User, string? Pass, string FromAddress);

public class MailKitEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, MailKit.Security.SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(options.User))
            await client.AuthenticateAsync(options.User, options.Pass, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
