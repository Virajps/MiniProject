using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Repositories.Models;
using Repositories.Services;
using StackExchange.Redis;
namespace Repositories.Services
{
    public class RedisUserService : IRedisUserService
    {
        private readonly IDatabase _database;

        public RedisUserService(IConnectionMultiplexer connectionMultiplexer)
        {
            _database = connectionMultiplexer.GetDatabase();
        }

        public async Task SetUserAsync(t_Employee user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var cacheUser = new t_Employee
            {
                EmployeeId = user.EmployeeId,
                Name = user.Name,
                Email = user.Email,
                Gender = user.Gender,
                Image = user.Image,
                Role = user.Role ?? "Employee",
                Status = user.Status
            };

            var redisKey = GetRedisKey(user.Email);
            var payload = JsonSerializer.Serialize(cacheUser);

            await _database.StringSetAsync(redisKey, payload, TimeSpan.FromMinutes(30));//Time for remove from cache
        }

        public async Task<t_Employee?> GetUserAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var redisValue = await _database.StringGetAsync(GetRedisKey(email));
            if (redisValue.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<t_Employee>(redisValue!);
        }

        private static string GetRedisKey(string email)
        {
            return $"user:{email.Trim().ToLowerInvariant()}";
        }
        public async Task SetOTP(string email, string otp)
        {
            await _database.StringSetAsync(email, otp, TimeSpan.FromMinutes(5));
        }

        public async Task<string> GetOTP(string email)
        {
            return await _database.StringGetAsync(email);
        }

        public async Task RemoveOTP(string email)
        {
            await _database.KeyDeleteAsync(email);
        }
    }
}

