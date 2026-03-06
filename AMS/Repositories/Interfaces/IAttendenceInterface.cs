using Repositories.Models;
namespace Repositories.Interfaces;

public interface IAttendenceInterface
{
     public Task<List<vm_TaskSummary>> GetEmployeeTaskSummary(int EmployeeId);
     public Task<vm_AttendenceSummary> GetEmployeeAttendanceSummary(int employeeId);
     public Task<vm_AttendanceChartResult> GetAttendanceChart(int empId, string type, DateTime date);
}
