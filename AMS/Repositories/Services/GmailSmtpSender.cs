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
      var subject = "Attendance Management System - Registration Successful";
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
          <title>Welcome to AMS</title>
        </head>

        <body style=""margin:0; padding:0; background-color:#ffffff; font-family:Arial, sans-serif;"">

        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:30px 10px;"">
          <tr>
            <td align=""center"">

              <!-- MAIN CARD -->
              <table width=""100%"" cellpadding=""0"" cellspacing=""0""
                    style=""max-width:600px; background:#ffffff; border-radius:10px; overflow:hidden; border:1px solid #e0e0e0;"">

                <!-- HEADER -->
                <tr>
                  <td style=""background:#039BE5; padding:25px; text-align:center;"">

                    <img src=""{logoUrl}"" width=""70"" style=""display:block; margin:0 auto 10px;"" />

                    <h2 style=""margin:0; color:#ffffff; font-size:22px;"">
                      Attendance Management System
                    </h2>

                    <p style=""margin:5px 0 0; color:#d6f0fb; font-size:13px;"">
                      Smart Attendance, Simplified
                    </p>

                  </td>
                </tr>

                <!-- CONTENT -->
                <tr>
                  <td style=""padding:30px 25px;"">

                    <h2 style=""margin-top:0; color:#039BE5; font-size:21px;"">
                      Welcome to AMS, {userName}!
                    </h2>

                    <p style=""color:#555; font-size:15px; line-height:1.6;"">
                      We're excited to have you onboard. AMS is designed to simplify attendance tracking
                      and improve workforce management with ease.
                    </p>

                    <p style=""color:#555; font-size:15px; line-height:1.6;"">
                      Once your account is approved by the admin, you will be able to mark attendance,
                      view reports, and manage your records efficiently.
                    </p>

                    <p style=""color:#555; font-size:15px; line-height:1.6;"">
                      Stay connected and get real-time updates about your attendance activity.
                    </p>

                    <!-- BUTTON -->
                    <div style=""text-align:center; margin:30px 0;"">
                      <a href=""{loginUrl}""
                        style=""background:#039BE5; color:#ffffff; padding:14px 30px; text-decoration:none; border-radius:6px; font-size:15px; display:inline-block;"">
                        Wait for Activation !!
                      </a>
                    </div>

                  </td>
                </tr>

                <!-- FOOTER -->
                <tr>
                  <td style=""background:#fafafa; text-align:center; padding:20px; font-size:12px; color:#777;"">

                    <p style=""margin:5px 0;"">
                      © 2026 Attendance Management System. All rights reserved.
                    </p>

                    <p style=""margin:5px 0;"">
                      Casepoint Pvt. Ltd., TGB Circle, Adajan, Surat
                    </p>

                    <!-- SOCIAL ICONS -->
                    <div style=""margin:15px 0;"">
                      <a href=""https://www.instagram.com/casepoint.hq/"" target=""_blank"" style=""margin:0 10px;"">
                        <img src=""https://cdn-icons-png.flaticon.com/512/2111/2111463.png"" width=""26"" />
                      </a>

                      <a href=""https://www.linkedin.com/company/casepoint-llc/"" target=""_blank"" style=""margin:0 10px;"">
                        <img src=""https://cdn-icons-png.flaticon.com/512/733/733561.png"" width=""26"" />
                      </a>

                      <a href=""https://x.com/Casepoint"" target=""_blank"" style=""margin:0 10px;"">
                        <img src=""https://cdn-icons-png.flaticon.com/512/5968/5968830.png"" width=""26"" />
                      </a>
                    </div>

                    <p style=""margin:5px 0; font-size:11px; color:#999;"">
                      If you did not create this account, please ignore this email.
                    </p>

                  </td>
                </tr>

              </table>

            </td>
          </tr>
        </table>

        </body>
        </html>";
    }

    public async Task SendStatusEmail(string toEmail, string userName, bool isActive)
    {
      var subject = isActive
          ? "Your Account is Now Active ✅"
          : "Your Account is Inactive ❌";

      var loginUrl = "http://localhost:5283/User/Login";
      var logoUrl = "https://res.cloudinary.com/dku9eh2pw/image/upload/v1773900932/logo_xlhf5r.png";

      var body = BuildStatusHtml(userName, loginUrl, logoUrl, isActive);

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

    private static string BuildStatusHtml(string userName, string loginUrl, string logoUrl, bool isActive)
    {
      var headerColor = isActive ? "#4CAF50" : "#E53935"; // Green / Red
      var buttonColor = headerColor;

      var title = isActive
          ? "Your Account is Activated 🎉"
          : "Your Account is Inactive ⚠️";

      var message = isActive
          ? "Your account has been successfully activated. You can now login and start using the system."
          : "Your account is currently inactive. Please contact the admin to activate your account.";

      var buttonText = isActive ? "Login Now" : "Contact Admin";

      return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""UTF-8"">
  <title>Status Update</title>
</head>

<body style=""margin:0; padding:0; background-color:#ffffff; font-family:Arial, sans-serif;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:30px 10px;"">
  <tr>
    <td align=""center"">

      <table width=""100%"" cellpadding=""0"" cellspacing=""0""
             style=""max-width:600px; background:#ffffff; border-radius:10px; overflow:hidden; border:1px solid #e0e0e0;"">

        <!-- HEADER -->
        <tr>
          <td style=""background:{headerColor}; padding:25px; text-align:center;"">
            <img src=""{logoUrl}"" width=""70"" style=""display:block; margin:0 auto 10px;"" />
            <h2 style=""margin:0; color:#ffffff;"">Attendance Management System</h2>
          </td>
        </tr>

        <!-- CONTENT -->
        <tr>
          <td style=""padding:30px 25px;"">

            <h2 style=""margin-top:0; color:{headerColor};"">
              Hello {userName},
            </h2>

            <h3 style=""color:{headerColor}; margin-bottom:15px;"">
              {title}
            </h3>

            <p style=""color:#555; font-size:15px; line-height:1.6;"">
              {message}
            </p>

            <div style=""text-align:center; margin:30px 0;"">
              <a href=""{loginUrl}""
                 style=""background:{buttonColor}; color:#ffffff; padding:14px 30px; text-decoration:none; border-radius:6px; display:inline-block;"">
                {buttonText}
              </a>
            </div>

          </td>
        </tr>

        <!-- FOOTER -->
        <tr>
          <td style=""background:#fafafa; text-align:center; padding:20px; font-size:12px; color:#777;"">
            <p>© 2026 Attendance Management System</p>
            <p>Casepoint Pvt. Ltd., Surat</p>
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
