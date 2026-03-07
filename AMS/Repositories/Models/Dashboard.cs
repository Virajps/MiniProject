public class DashboardModel
{
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }

    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }

    public List<TaskChartModel> TaskHours { get; set; }
    public List<AttendanceModel> RecentAttendance { get; set; }
}
public class TaskChartModel
{
    public string Task { get; set; }
    public int Hours { get; set; }
}

public class AttendanceModel
{
    public string EmployeeName { get; set; }
    public string CheckIn { get; set; }
    public string CheckOut { get; set; }
    public string Status { get; set; }
}