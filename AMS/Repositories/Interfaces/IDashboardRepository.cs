using Repositories.Models;

public interface IDashboardRepository
{
    DashboardModel GetDashboardData();
    public Task<List<AccessModel>> GetAllUsersForAccess();
    public  Task<vm_EmployeeProgressReport> GetEmployeeProgress(int empId,int month,int year);
}