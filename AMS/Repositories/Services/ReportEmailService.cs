using Repositories.Interfaces;

namespace Repositories.Services
{
  public class ReportEmailService
  {
    private readonly IGmailSmtpSenderInterface _emailSender;

    public ReportEmailService(IGmailSmtpSenderInterface emailSender)
    {
      _emailSender = emailSender;
    }

    public async Task SendProgressReportEmail(string toEmail, string userName, byte[] pdfBytes, string fileName)
    {
      var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "ProgressReport.pdf" : fileName;
      var subject = "Attendance Management System - Progress Report";
      var logoUrl = "https://res.cloudinary.com/dku9eh2pw/image/upload/v1773915524/AMSLogo_bsxqo2.png";
      var body = BuildProgressReportHtml(userName, logoUrl, safeFileName);

      await _emailSender.SendEmailWithAttachment(
        toEmail,
        userName,
        subject,
        body,
        pdfBytes,
        safeFileName,
        "application/pdf");
    }

    private static string BuildProgressReportHtml(string userName, string logoUrl, string fileName)
    {
      var displayName = string.IsNullOrWhiteSpace(userName) ? "Employee" : userName;

      return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset=""UTF-8"">
  <title>Progress Report</title>
</head>

<body style=""margin:0; padding:0; background-color:#ffffff; font-family:Arial, sans-serif;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:30px 10px;"">
  <tr>
    <td align=""center"">

      <table width=""100%"" cellpadding=""0"" cellspacing=""0""
             style=""max-width:600px; background:#ffffff; border-radius:10px; overflow:hidden; border:1px solid #e0e0e0;"">

        <tr>
          <td style=""background:#039BE5; padding:25px; text-align:center;"">
            <img src=""{logoUrl}"" width=""70"" style=""display:block; margin:0 auto 10px;"" />
            <h2 style=""margin:0; color:#ffffff; font-size:22px;"">Attendance Management System</h2>
            <p style=""margin:5px 0 0; color:#d6f0fb; font-size:13px;"">Employee Progress Report</p>
          </td>
        </tr>

        <tr>
          <td style=""padding:30px 25px;"">

            <h2 style=""margin-top:0; color:#039BE5; font-size:21px;"">Hello {displayName},</h2>

            <p style=""color:#555; font-size:15px; line-height:1.6;"">
              Your progress report is attached to this email in PDF format.
            </p>

            <p style=""color:#555; font-size:15px; line-height:1.6;"">
              You can download the report from this email and keep it for your records.
            </p>

            <div style=""text-align:center; margin:25px 0;"">
              <span style=""display:inline-block; font-size:14px; padding:10px 16px; border:1px dashed #039BE5; border-radius:6px; color:#039BE5;"">
                Attachment: {fileName}
              </span>
            </div>

            <p style=""color:#777; font-size:13px; line-height:1.6;"">
              If you have any questions about this report, please contact the admin.
            </p>

          </td>
        </tr>

        <tr>
          <td style=""background:#fafafa; text-align:center; padding:20px; font-size:12px; color:#777;"">
            <p style=""margin:5px 0;"">(c) 2026 Attendance Management System</p>
            <p style=""margin:5px 0;"">Casepoint Pvt. Ltd., Surat</p>
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
