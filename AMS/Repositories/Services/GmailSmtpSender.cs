using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using Repositories.Models;
using Repositories.Interfaces;

namespace Repositories.Services
{
    public class GmailSmtpSender : IGmailSmtpSenderInterface
    {
        private readonly EmailSettings _settings;

        public GmailSmtpSender(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task Welcome(string toEmail, string userName)
        {
            var subject = "Attendance System - Registration Successful";
            var loginUrl = "http://localhost:5283/User/Login";
            var logoUrl = "https://res.cloudinary.com/dku9eh2pw/image/upload/v1773900932/logo_xlhf5r.png";
            var body = BuildRegistrationHtml(userName, loginUrl, logoUrl);

            ValidateOptions(toEmail, subject, body);

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };
            message.To.Add(new MailAddress(toEmail, userName));

            using var smtp = new SmtpClient(_settings.SmtpServer, _settings.Port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.AppPassword)
            };

            await smtp.SendMailAsync(message);
        }

        private void ValidateOptions(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(_settings.SmtpServer))
                throw new InvalidOperationException("EmailSettings:SmtpServer is required.");
            if (_settings.Port <= 0)
                throw new InvalidOperationException("EmailSettings:Port must be a positive number.");
            if (string.IsNullOrWhiteSpace(_settings.SenderName))
                throw new InvalidOperationException("EmailSettings:SenderName is required.");
            if (string.IsNullOrWhiteSpace(_settings.SenderEmail))
                throw new InvalidOperationException("EmailSettings:SenderEmail is required.");
            if (string.IsNullOrWhiteSpace(_settings.AppPassword))
                throw new InvalidOperationException("EmailSettings:AppPassword is required.");
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new InvalidOperationException("Recipient email is required.");
            if (string.IsNullOrWhiteSpace(subject))
                throw new InvalidOperationException("Email subject is required.");
            if (string.IsNullOrWhiteSpace(body))
                throw new InvalidOperationException("Email body is required.");
        }

        private static string BuildRegistrationHtml(string userName, string loginUrl, string logoUrl)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""UTF-8"">
  <title>Registration Successful</title>
</head>

<body style=""margin:0; padding:0; background-color:#eeeeee; font-family:Arial, sans-serif;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:20px;"">
  <tr>
    <td align=""center"">

      <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff; border-radius:6px; overflow:hidden;"">


        <!-- Header -->
        <tr>
          <td style=""background:#4FC3F7; color:#ffffff; text-align:center; padding:15px;"">
            <h2 style=""margin:0;"">Attendance System - Registration Successful</h2>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style=""padding:30px; text-align:center;"">

            <img src=""{logoUrl}"" width=""80"" height=""80"" style=""display:block; width:80px; height:80px; object-fit:contain; border:0; margin:0 auto 20px;"" alt=""AMS Logo"" />

            <h3>Hello {userName},</h3>

            <p>Your registration was successful.</p>

            <p>Your account is currently under admin review. You will be notified once activated.</p>

            <div style=""margin:25px 0;"">
              <a href=""{loginUrl}""
                 style=""background:#4FC3F7; color:#fff; padding:12px 25px; text-decoration:none; border-radius:4px;"">
                Wait for Activation
              </a>
            </div>

          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style=""background:#f5f5f5; text-align:center; padding:20px; font-size:12px; color:#777;"">
            Casepoint Pvt. Ltd., TGB Circle, Adajan, Surat

            <div style=""margin:15px 0;"">
              <a href=""https://www.facebook.com/Casepoint.HQ"" target=""_blank""><img src=""https://cdn-icons-png.flaticon.com/512/733/733547.png"" width=""24"" /></a>
              <a href=""https://www.twitter.com/Casepoint"" target=""_blank""><img src=""https://cdn-icons-png.flaticon.com/512/5968/5968830.png"" width=""24"" /></a>
              <a href=""https://www.casepoint.com/"" target=""_blank""><img src=""https://cdn-icons-png.flaticon.com/512/733/733558.png"" width=""24"" /></a>
              <a href=""https://www.linkedin.com/company/casepoint-llc"" target=""_blank""><img src=""https://cdn-icons-png.flaticon.com/512/733/733561.png"" width=""24"" /></a>
            </div>

            &copy; 2026 Attendance Management System
          </td>
        </tr>

      </table>

    </td>
  </tr>
</table>

</body>
</html>";
        }
    }
}
