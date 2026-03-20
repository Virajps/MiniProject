using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IUserInterface
    {
        public Task<t_Employee> LoginUser(vm_login login);

        public Task<int> RegisterUser(t_Employee employee);
        Task<bool> UpdatePassword(string email, string password);
    }
}