using System.ComponentModel.DataAnnotations;
using Repositories.Models;
namespace Repositories.Models;

public class vm_AttendenceSummary
{
  public int EmpId { get; set; }
  public string? Name { get; set; }
  public string? Image { get; set; }
  public string? Email { get; set; }
  public string? Gender { get; set; }

  public int PresentCount { get; set; }
  public int AbsentCount { get; set; }
  public int TotalWorkingHours { get; set; }
  public int LateInCount { get; set; }
  public int EarlyOutCount { get; set; }

  public List<vm_TaskSummary> TaskSummaries { get; set; } = new();
  public List<t_Attendance> AttendanceHistory { get; set; } = new();

  // For today's attendance
  public t_Attendance? TodayRecord { get; set; }
}

public class vm_AttendanceChart
{
  public string? Label { get; set; }
  public int Hours { get; set; }
}

public class vm_AttendanceChartResult
{
  public int TotalHours { get; set; }
  public List<vm_AttendanceChart> ChartData { get; set; } = new();
}

public class vm_AttendanceScheduler
{
  public int Id { get; set; }

  public string? Title { get; set; }

  public DateTime Start { get; set; }

  public DateTime End { get; set; }

  public string? Status { get; set; }

  public string? WorkType { get; set; }

  public string? TaskType { get; set; }

  public int WorkingHour { get; set; }
}
