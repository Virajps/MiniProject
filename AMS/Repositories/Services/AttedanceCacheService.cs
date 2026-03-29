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

        public async Task SetClockInAsync(int employeeId,string ename, DateTime clockInTime, string workType, string status)
        {
            var cacheEntry = new CacheAttendanceClockIn
            {
                EmployeeId = employeeId,
                EmployeeName=ename,
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

        private static string GetEmployeeNameKey(int employeeId)
        {
            return $"attendance:employee:name:{employeeId}";
        }
        public async Task SetEmployeeNameAsync(int employeeId, string employeeName)
        {
            if (employeeId <= 0 || string.IsNullOrWhiteSpace(employeeName))
            {
                return;
            }

            await _database.StringSetAsync(
                GetEmployeeNameKey(employeeId),
                employeeName.Trim(),
                TimeSpan.FromDays(1));
        }
        public async Task<string?> GetEmployeeNameAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                return null;
            }

            var cachedValue = await _database.StringGetAsync(GetEmployeeNameKey(employeeId));
            return cachedValue.IsNullOrEmpty ? null : cachedValue.ToString();
        }

        public async Task RemoveEmployeeNameAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                return;
            }

            await _database.KeyDeleteAsync(GetEmployeeNameKey(employeeId));
        }
    }
}


