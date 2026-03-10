namespace Repositories.Models
{
    public class vm_EmployeeProgressReport
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }

        public int Present { get; set; }
        public int LateIn { get; set; }
        public int EarlyOut { get; set; }
        public int Absent { get; set; }

        public int TotalWorkingHours { get; set; }

        public List<TaskSummary> Tasks { get; set; } = new();
    }

    public class TaskSummary
    {
        public string TaskType { get; set; }
        public int Hours { get; set; }
    }
}