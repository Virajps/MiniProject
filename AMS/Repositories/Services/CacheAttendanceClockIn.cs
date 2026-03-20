using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Services
{
    public class CacheAttendanceClockIn
    {
        public int EmployeeId { get; set; }
        public DateTime ClockInTime { get; set; }
        public string WorkType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;   
    }
}