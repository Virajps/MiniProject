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
        Task SetOTP(string email, string otp);
        Task<string> GetOTP(string email);
        Task RemoveOTP(string email);
    }
}