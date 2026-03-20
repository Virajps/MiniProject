using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Repositories.Interfaces;
using StackExchange.Redis;

namespace Repositories.Services
{
    public class AttedanceCacheService:IAttedanceCacheService
    {
        private readonly IDatabase _database;

        public AttedanceCacheService(IConnectionMultiplexer connectionMultiplexer)
        {
            _database = connectionMultiplexer.GetDatabase();
        }

        public async Task SetClockInAsync(int employeeId, DateTime clockInTime, string workType, string status)
        {
            var cacheEntry = new CacheAttendanceClockIn
            {
                EmployeeId = employeeId,
                ClockInTime = clockInTime,
                WorkType = workType,
                Status = status
            };

            await _database.StringSetAsync(
                GetKey(employeeId),
                JsonSerializer.Serialize(cacheEntry),
                TimeSpan.FromDays(1));
        }

        public async Task<CacheAttendanceClockIn?> GetClockInAsync(int employeeId)
        {
            var cachedValue = await _database.StringGetAsync(GetKey(employeeId));
            if (cachedValue.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CacheAttendanceClockIn>(cachedValue!);
        }

        public async Task RemoveClockInAsync(int employeeId)
        {
            await _database.KeyDeleteAsync(GetKey(employeeId));
        }

        private static string GetKey(int employeeId)
        {
            return $"attendance:clockin:{employeeId}";
        }
    }
}


