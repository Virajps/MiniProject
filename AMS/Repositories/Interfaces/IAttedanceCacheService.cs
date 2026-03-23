using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Services;

namespace Repositories.Interfaces
{
    public interface IAttedanceCacheService
    {
        Task SetClockInAsync(int employeeId,string ename, DateTime clockInTime, string workType, string status);
        Task<CacheAttendanceClockIn?> GetClockInAsync(int employeeId);
        Task RemoveClockInAsync(int employeeId);
        Task SetEmployeeNameAsync(int employeeId, string employeeName);
        Task<string?> GetEmployeeNameAsync(int employeeId);
        Task RemoveEmployeeNameAsync(int employeeId);
    }
}