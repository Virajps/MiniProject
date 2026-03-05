using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IUserInterface
    {
        public Task<List<t_Employee>> LoginUser(vm_login login);

        public Task<int> RegisterUser(t_Employee employee);
    }
}