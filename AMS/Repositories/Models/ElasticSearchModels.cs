namespace Repositories.Models
{
    public class AttendanceElasticDocument
    {
        public int AttendId { get; set; }
        public int EmpId { get; set; }
        public DateTime AttendDate { get; set; }
        public int ClockInHour { get; set; }
        public int ClockInMin { get; set; }
        public int ClockOutHour { get; set; }
        public int ClockOutMin { get; set; }
        public int WorkingHour { get; set; }
        public string AttendStatus { get; set; } = "Regular";
        public string WorkType { get; set; } = string.Empty;
        public string RawTaskType { get; set; } = string.Empty;
        public List<string> TaskTypes { get; set; } = new();
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeEmail { get; set; } = string.Empty;
        public string EmployeeStatus { get; set; } = string.Empty;

        public static AttendanceElasticDocument FromAttendance(
            t_Attendance attendance,
            string? employeeName,
            string? employeeEmail,
            string? employeeStatus)
        {
            return new AttendanceElasticDocument
            {
                AttendId = attendance.AttendId,
                EmpId = attendance.EmpId,
                AttendDate = attendance.AttendDate.Date,
                ClockInHour = attendance.ClockInHour ?? 0,
                ClockInMin = attendance.ClockInMin ?? 0,
                ClockOutHour = attendance.ClockOutHour ?? 0,
                ClockOutMin = attendance.ClockOutMin ?? 0,
                WorkingHour = attendance.WorkingHour ?? 0,
                AttendStatus = attendance.AttendStatus ?? "Regular",
                WorkType = attendance.WorkType ?? string.Empty,
                RawTaskType = attendance.TaskType ?? string.Empty,
                TaskTypes = (attendance.TaskType ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                EmployeeName = employeeName ?? attendance.EmployeeName ?? string.Empty,
                EmployeeEmail = employeeEmail ?? string.Empty,
                EmployeeStatus = employeeStatus ?? string.Empty
            };
        }
    }

    public class EmployeeElasticInfo
    {
        public string? EmployeeName { get; set; }
        public string? EmployeeEmail { get; set; }
        public string? EmployeeStatus { get; set; }
    }

    public class EmployeeFilterRequest
    {
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeStatus { get; set; }
        public string? WorkType { get; set; }
        public string? TaskType { get; set; }
        public string? AttendStatus { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class EmployeeFilterResult
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? Email { get; set; }
        public string? EmployeeStatus { get; set; }
        public int TotalAttendance { get; set; }
        public int TotalWorkingHours { get; set; }
        public int LateInCount { get; set; }
        public int EarlyOutCount { get; set; }
        public List<string> WorkTypes { get; set; } = new();
        public List<string> TaskTypes { get; set; } = new();
    }

    public class AttendanceAnalysisRequest
    {
        public int? EmployeeId { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class AnalysisBucketResult
    {
        public string Name { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public int EmployeeCount { get; set; }
        public int TotalWorkingHours { get; set; }
        public double AverageWorkingHours { get; set; }
    }

    public class TaskHourAllocation
    {
        public string TaskType { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }
}
