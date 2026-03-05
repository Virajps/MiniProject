using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IUserInterface
    {
        public Task<List<t_Employee>> LoginUser(string Email, string Password);

        public Task<int> RegisterUser(t_Employee employee);
    }
}