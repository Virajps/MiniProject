using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Repositories;

public class OTPEmailService
{
  private readonly IConfiguration _config;

  public OTPEmailService(IConfiguration config)
  {
    _config = config;
  }

  public async Task SendOTPEmail(string toEmail, string otp)
  {
    var smtp = _config["EmailSettings:SmtpServer"];
    var port = int.Parse(_config["EmailSettings:Port"]);
    var senderEmail = _config["EmailSettings:SenderEmail"];
    var password = _config["EmailSettings:SenderPassword"];
    var senderName = _config["EmailSettings:SenderName"];

    // ✅ Build HTML Body
    var body = BuildOtpHtml(otp);

    using var message = new MailMessage
    {
      From = new MailAddress(senderEmail, senderName),
      Subject = "Password Reset OTP 🔐",
      Body = body,
      IsBodyHtml = true,
      BodyEncoding = Encoding.UTF8,
      SubjectEncoding = Encoding.UTF8
    };

    message.To.Add(toEmail);

    using var client = new SmtpClient(smtp, port)
    {
      Credentials = new NetworkCredential(senderEmail, password),
      EnableSsl = true,
      UseDefaultCredentials = false,
      DeliveryMethod = SmtpDeliveryMethod.Network
    };

    await client.SendMailAsync(message);
  }

  // ✅ HTML TEMPLATE (Professional UI)
  private string BuildOtpHtml(string otp)
  {
    return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
</head>

<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial'>

<table width='100%' style='padding:30px 10px'>
<tr>
<td align='center'>

<table width='100%' style='max-width:600px;background:#fff;border-radius:10px;border:1px solid #ddd'>

<tr>
<td style='background:#4CAF50;padding:20px;text-align:center;color:white'>
<h2>Attendance Management System</h2>
</td>
</tr>

<tr>
<td style='padding:30px;text-align:center'>

<h3>Password Reset OTP</h3>

<p>Your OTP is:</p>

<div style='font-size:30px;font-weight:bold;color:#4CAF50;margin:20px 0'>
{otp}
</div>

<p>This OTP is valid for <b>5 minutes</b></p>

<p style='color:#888'>If you didn't request this, ignore this email.</p>

</td>
</tr>

<tr>
<td style='background:#f1f1f1;text-align:center;padding:15px;font-size:12px;color:#777'>
© 2026 AMS | Surat
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