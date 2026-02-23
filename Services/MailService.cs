using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using AccommodationSystem.Data;

namespace AccommodationSystem.Services
{
    public static class MailService
    {
        public static async Task SendReceiptAsync(string toEmail, string subject, string body, byte[] pdfBytes, string fileName)
        {
            var settings = DatabaseService.GetSettings();
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.PropertyName, settings.SmtpUser));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { TextBody = body };
            builder.Attachments.Add(fileName, pdfBytes, ContentType.Parse("application/pdf"));
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }
    }
}
