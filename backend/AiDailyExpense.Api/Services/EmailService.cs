using System.Net;
using System.Net.Mail;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendCodeAsync(string toEmail, string subject, string code)
        {
            var server = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var port = int.TryParse(_configuration["EmailSettings:SmtpPort"], out var parsedPort) ? parsedPort : 587;
            var senderName = _configuration["EmailSettings:SenderName"] ?? "QuanLyChiTieu";
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
            {
                throw new InvalidOperationException("Chưa cấu hình email gửi mã xác thực.");
            }

            using var client = new SmtpClient(server, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = $"Mã xác thực của bạn là: {code}\n\nMã này sẽ hết hạn sau 10 phút.",
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
    }
}
