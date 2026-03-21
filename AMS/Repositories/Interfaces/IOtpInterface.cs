namespace Repositories.Interfaces;

public interface IOtpInterface
{
  Task SendOtpEmail(string toEmail, string userName, string otp);
}
