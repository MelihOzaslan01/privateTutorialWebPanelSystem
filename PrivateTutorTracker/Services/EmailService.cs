using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PrivateTutorTracker.Models;

namespace PrivateTutorTracker.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
    Task<bool> SendBulkEmailAsync(IEnumerable<string> emailAddresses, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var message = CreateMessage(toEmail, toName, subject, htmlBody);
            await SendMessageAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email could not be sent to {Email}.", toEmail);
            return false;
        }
    }

    public async Task<bool> SendBulkEmailAsync(IEnumerable<string> emailAddresses, string subject, string htmlBody)
    {
        var allSent = true;

        foreach (var email in emailAddresses.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct())
        {
            allSent &= await SendEmailAsync(email, email, subject, htmlBody);
        }

        return allSent;
    }

    private MimeMessage CreateMessage(string toEmail, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();
        return message;
    }

    private async Task SendMessageAsync(MimeMessage message)
    {
        using var client = new SmtpClient();
        var secureSocketOptions = _settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureSocketOptions);

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
