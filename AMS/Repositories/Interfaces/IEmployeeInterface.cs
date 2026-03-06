using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IEmployeeInterface
    {
        public Task<List<t_Employee>> GetAllUsers();

        public Task<t_Employee> GetUserById(int EmployeeId);

        public Task<t_Employee> UpdateUser(int EmployeeId, t_Employee employee);

        public Task<int> DeleteUser(int EmployeeId);

        public Task<t_Employee> UpdateUserStatus(int EmployeeId, string Status);

        public Task<int> ChangePassword(vm_ChangePassword changePassword);

       
    }
}