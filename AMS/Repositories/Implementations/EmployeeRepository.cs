using Npgsql;
using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Implementations
{
    public class EmployeeRepository : IEmployeeInterface
    {
        private readonly NpgsqlConnection _conn;
        public EmployeeRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }
        public async Task<int> DeleteUser(int EmployeeId)
        {
            try{
            var cmd = new NpgsqlCommand("DELETE FROM t_employee WHERE c_empid = @EmployeeId", _conn);
            cmd.Parameters.AddWithValue("@EmployeeId", EmployeeId);
            await _conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            await _conn.CloseAsync();
            return 1;
            }
            catch(Exception ex)
            {
                Console.WriteLine("DeleteUser Error: " + ex.Message);
                return 0;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        public async Task<List<t_Employee>> GetAllUsers()
        {
            var employees = new List<t_Employee>();
            try
            {
                using var cmd = new NpgsqlCommand(
                    "SELECT c_empid, c_name, c_email, c_role, c_profileimage FROM t_employee ORDER BY c_empid",
                    _conn);

                await _conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    employees.Add(new t_Employee
                    {
                        EmployeeId = reader.GetInt32(reader.GetOrdinal("c_empid")),
                        Name = reader["c_name"]?.ToString(),
                        Email = reader["c_email"]?.ToString(),
                        Role = reader["c_role"]?.ToString(),
                        Image = reader["c_profileimage"]?.ToString(),
                        Status = reader["c_status"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetAllEmployeeProfiles Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return employees;

        }

        public async Task<t_Employee> GetUserById(int EmployeeId)
        {
            try
            {
                using var cmd = new NpgsqlCommand(
                    "SELECT c_empid, c_name, c_email, c_role, c_profileimage FROM t_employee WHERE c_empid = @empid",
                    _conn);
                cmd.Parameters.AddWithValue("@empid", EmployeeId);

                await _conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new t_Employee
                    {
                        EmployeeId = reader.GetInt32(reader.GetOrdinal("c_empid")),
                        Name = reader["c_name"]?.ToString(),
                        Email = reader["c_email"]?.ToString(),
                        Role = reader["c_role"]?.ToString(),
                        Image = reader["c_profileimage"]?.ToString(),
                        Status = reader["c_status"]?.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetEmployeeProfileById Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return null;
        }

        public Task<t_Employee> UpdateUser(int EmployeeId, t_Employee employee)
        {
            throw new NotImplementedException();
        }

        public Task<int> UpdateUserStatus(int EmployeeId, string Status)
        {
            throw new NotImplementedException();
        }

        public async Task<int> ChangePassword(vm_ChangePassword changePassword)
        {
             try
        {
            var query = changePassword.Image is null
                ? "UPDATE t_employee SET c_name = @name WHERE c_empid = @empid"
                : "UPDATE t_employee SET c_name = @name, c_profileimage = @profileimage WHERE c_empid = @empid";

            using var cmd = new NpgsqlCommand(query, _conn);
            cmd.Parameters.AddWithValue("@name", changePassword.Name);
            cmd.Parameters.AddWithValue("@empid", changePassword.EmployeeId);
            if (changePassword.Image is not null)
            {
                cmd.Parameters.AddWithValue("@profileimage", changePassword.Image);
            }

            await _conn.OpenAsync();
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("UpdateProfile Error: " + ex.Message);
            return 0;
        }
        finally
        {
            await _conn.CloseAsync();
        }
        }
    }
}