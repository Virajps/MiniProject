using Npgsql;
using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Implementations
{
    public class UserRepository : IUserInterface
    {

        private readonly NpgsqlConnection _conn;
        public UserRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }
        public Task<List<t_Employee>> LoginUser(string Email, string Password)
        {
            throw new NotImplementedException();
        }

        public async Task<int> RegisterUser(t_Employee employee)
        {
            try
        {
            await _conn.CloseAsync();

            using (var comcheck = new NpgsqlCommand(
                       "SELECT 1 FROM t_employee WHERE c_email = @c_email",
                       _conn))
            {
                comcheck.Parameters.AddWithValue("@c_email", employee.Email ?? "");

                await _conn.OpenAsync();
                using var reader = await comcheck.ExecuteReaderAsync();

                if (reader.HasRows)
                {
                    await _conn.CloseAsync();
                    return 0;
                }
            }

            await _conn.CloseAsync();

            using (var com = new NpgsqlCommand(
                       @"INSERT INTO t_employee
                    (c_name, c_email, c_password, c_role, c_image)
                    VALUES
                    (@c_name, @c_email, @c_password, @c_role, @c_image)", _conn))
            {
                com.Parameters.AddWithValue("@c_name", (object?)employee.Name ?? DBNull.Value);
                com.Parameters.AddWithValue("@c_email", employee.Email ?? "");
                com.Parameters.AddWithValue("@c_password", employee.Password ?? "");
                com.Parameters.AddWithValue("@c_role", "employee");
                com.Parameters.AddWithValue("@c_image", (object?)employee.Image ?? DBNull.Value);
                await _conn.OpenAsync();
                await com.ExecuteNonQueryAsync();
                await _conn.CloseAsync();
            }

            return 1;
        }
        catch (Exception ex)
        {
            await _conn.CloseAsync();
            Console.WriteLine("Register Failed: " + ex.Message);
            return -1;
        }
        }
    }
}