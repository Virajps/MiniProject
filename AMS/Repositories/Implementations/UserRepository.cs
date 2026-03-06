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
        public async Task<t_Employee> LoginUser(vm_login login)
        {
            var userData = new t_Employee();

        try
        {
            var qry = @"SELECT * FROM t_employee 
                            WHERE c_email=@c_email 
                            AND c_password=@c_password";

            using var cmd = new NpgsqlCommand(qry, _conn);

            cmd.Parameters.AddWithValue("@c_email", login.UserEmail ?? "");
            cmd.Parameters.AddWithValue("@c_password", login.UserPassword ?? "");

            await _conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                userData.EmployeeId = reader.GetInt32(reader.GetOrdinal("c_empid"));
                userData.Name = reader["c_name"]?.ToString();
                userData.Email = reader["c_email"]?.ToString();
                userData.Role = reader["c_role"]?.ToString();
                userData.Image = reader["c_image"]?.ToString();
                userData.Status = reader["c_status"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Login Error: " + ex.Message);
        }
        finally
        {
            await _conn.CloseAsync();
        }

        return userData;
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