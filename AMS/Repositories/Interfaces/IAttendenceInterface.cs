namespace Repositories.Interfaces;

public interface IAttendenceInterface
{
     public Task<List<vm_TaskSummary>> GetEmployeeTaskSummary(int EmployeeId);
}
