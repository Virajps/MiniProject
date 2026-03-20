using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Interfaces
{
    public interface IGmailSmtpSenderInterface
    {
        Task Welcome(string toEmail, string userName);
        Task SendStatusEmail(string toEmail, string userName, bool isActive);
        Task SendOtpEmail(string toEmail, string userName, string otp);
    }
}
