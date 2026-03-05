using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Implementations
{
    public class EmployeeRepository : IEmployeeInterface
    {
        public Task DeleteUser(int EmployeeId)
        {
            throw new NotImplementedException();
        }

        public Task<t_Employee> GetAllUsers()
        {
            throw new NotImplementedException();
        }

        public Task<t_Employee> GetUserById(int EmployeeId)
        {
            throw new NotImplementedException();
        }

        public Task<t_Employee> UpdateUser(int EmployeeId, t_Employee employee)
        {
            throw new NotImplementedException();
        }
    }
}