using System.ComponentModel.DataAnnotations;

namespace Repositories.Models;

public class t_Attendence
{
        public int AttendId { get; set; }
        public int EmpId { get; set; }
        public DateTime AttendDate { get; set; }

        // Clock In
        public int? ClockInHour { get; set; }
        public int? ClockInMin { get; set; }

        // Clock Out
        public int? ClockOutHour { get; set; }
        public int? ClockOutMin { get; set; }

        public int? WorkingHour { get; set; }

        // Regular / LateIn / EarlyOut
        public string? AttendStatus { get; set; }

        // Remote / Office / Field
        [Required(ErrorMessage = "Work type is required")]
        public string? WorkType { get; set; }

        // Comma-separated: Developing,Designing,Research
        public string? TaskType { get; set; }

        // For multi-select checkboxes (not stored directly)
        public List<string>? SelectedTasks { get; set; }

        // Navigation / display
        public string? EmployeeName { get; set; }

        // Computed helpers
        public string ClockInDisplay =>
            (ClockInHour.HasValue && ClockInMin.HasValue)
                ? $"{ClockInHour:D2}:{ClockInMin:D2}"
                : "--:--";

        public string ClockOutDisplay =>
            (ClockOutHour.HasValue && ClockOutMin.HasValue)
                ? $"{ClockOutHour:D2}:{ClockOutMin:D2}"
                : "--:--";
}
