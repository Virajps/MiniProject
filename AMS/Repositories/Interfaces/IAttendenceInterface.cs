using Repositories.Models;
namespace Repositories.Interfaces;

public interface IAttendenceInterface
{
     public Task<List<vm_TaskSummary>> GetEmployeeTaskSummary(int EmployeeId);
     public Task<vm_AttendenceSummary> GetEmployeeAttendanceSummary(int employeeId);
     public Task<vm_AttendanceChartResult> GetAttendanceChart(int empId, string type, DateTime date);
     public Task<int> ClockIn(int empId, string workType);
     public Task<int> ClockOut(int empId, List<string> taskTypes);
     public Task<List<vm_TaskSummary>> GetAllTaskSummary();
     public Task<List<vm_AttendanceScheduler>> GetAttendanceScheduler(int empId);
}
