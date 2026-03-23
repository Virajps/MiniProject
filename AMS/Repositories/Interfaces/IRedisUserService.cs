using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models;

namespace Repositories.Services
{
    public interface IRedisUserService
    {
        Task SetUserAsync(t_Employee user);
        Task<t_Employee?> GetUserAsync(string email);
         Task<t_Employee?> GetUserByIdAsync(int employeeId);
         Task RemoveUserAsync(string email);
        Task RemoveUserByIdAsync(int employeeId);
        public Task<t_Employee?> GetUserByIdAsync(int employeeId);
        Task SetOTP(string email, string otp);
        Task<string> GetOTP(string email);
        Task RemoveOTP(string email);
        Task SetOtpVerified(string email);
        Task<bool> IsOtpVerified(string email);
        Task RemoveOtpVerified(string email);

    }
}
