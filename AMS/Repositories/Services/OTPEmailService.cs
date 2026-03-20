using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Services
{
  public class OTPEmailService
  {
    private readonly IRedisUserService _redis;
    private readonly IUserInterface _repo;
    private readonly IGmailSmtpSenderInterface _email;

    public OTPEmailService(
        IRedisUserService redis,
        IUserInterface repo,
        IGmailSmtpSenderInterface email)
    {
      _redis = redis;
      _repo = repo;
      _email = email;
    }

    // ✅ STEP 1: SEND OTP
    public async Task<bool> SendOTP(string email, string userName)
    {
      var otp = new Random().Next(100000, 999999).ToString();

      // Save in Redis
      await _redis.SetOTP(email, otp);
      await _redis.RemoveOtpVerified(email);

      // Send Email
      await _email.SendOtpEmail(email, userName, otp);

      return true;
    }

    // ✅ STEP 2: VERIFY OTP
    public async Task<bool> VerifyOTP(string email, string otp)
    {
      var storedOtp = await _redis.GetOTP(email);

      if (storedOtp == null)
        return false;

      if (storedOtp == otp)
      {
        await _redis.SetOtpVerified(email);
        return true;
      }

      return false;
    }

    // ✅ STEP 3: RESET PASSWORD
    public async Task<bool> ResetPassword(string email, string password)
    {
      var isVerified = await _redis.IsOtpVerified(email);
      if (!isVerified)
      {
        return false;
      }

      var result = await _repo.UpdatePassword(email, password);

      if (result)
      {
        await _redis.RemoveOTP(email);
        await _redis.RemoveOtpVerified(email);
        return true;
      }

      return false;
    }
  }
}
