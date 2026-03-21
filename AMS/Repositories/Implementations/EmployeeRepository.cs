using Npgsql;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Services;

namespace Repositories.Implementations
{
    public class EmployeeRepository : IEmployeeInterface
    {
        private readonly NpgsqlConnection _conn;
        private readonly ElasticSearchService? _elasticSearchService;
        
        public EmployeeRepository(NpgsqlConnection conn, ElasticSearchService? elasticSearchService = null)
        {
            _conn = conn;
            _elasticSearchService = elasticSearchService;
        }
        public async Task<int> DeleteUser(int EmployeeId)
        {
            try
            {
                var cmd = new NpgsqlCommand("DELETE FROM t_employee WHERE c_empid = @EmployeeId", _conn);
                cmd.Parameters.AddWithValue("@EmployeeId", EmployeeId);
                await _conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                await _conn.CloseAsync();
                return 1;
            }
            catch (Exception ex)
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
                    "SELECT c_empid, c_name, c_email, c_gender, c_role, c_image, c_status FROM t_employee WHERE c_role = 'Employee' ORDER BY c_empid",
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
                        Gender = reader["c_gender"]?.ToString(),
                        Role = reader["c_role"]?.ToString(),
                        Image = reader["c_image"]?.ToString(),
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
                    "SELECT c_empid, c_name, c_email, c_password, c_gender, c_role, c_image, c_status FROM t_employee WHERE c_empid = @empid",
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
                        Password = reader["c_password"]?.ToString(),
                        Gender = reader["c_gender"]?.ToString(),
                        Role = reader["c_role"]?.ToString(),
                        Image = reader["c_image"]?.ToString(),
                        Status = reader["c_status"]?.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetEmployeeProfileById Error: " + ex.Message);
                return null!;
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return null!;
        }

        public async Task<bool> UpdateUser(int EmployeeId, t_Employee employee)
        {
            try
            {
                // Update employee data
                using var cmd = new NpgsqlCommand(
                    "UPDATE t_employee SET c_name=@name, c_gender=@gender, c_image=@image WHERE c_empid=@empid",
                    _conn);

                cmd.Parameters.AddWithValue("@empid", EmployeeId);
                cmd.Parameters.AddWithValue("@name", employee.Name ?? "");
                cmd.Parameters.AddWithValue("@gender", employee.Gender ?? "");
                cmd.Parameters.AddWithValue("@image", employee.Image ?? "");

                await _conn.OpenAsync();
                int rows = await cmd.ExecuteNonQueryAsync();
                await _conn.CloseAsync();  // Close connection before re-using it

                // Re-index all attendance records in ElasticSearch when employee info is updated
                if (rows > 0 && _elasticSearchService != null)
                {
                    try
                    {
                        // Get fresh employee data
                        var updatedEmp = await GetUserById(EmployeeId);
                        if (updatedEmp != null)
                        {
                            // Fetch all attendance records for this employee from database
                            await _conn.CloseAsync();  // Ensure connection is closed
                            
                            using var attCmd = new NpgsqlCommand(
                                @"SELECT c_attendid, c_empid, c_attenddate, c_clockinhour, c_clockinmin, c_clockouthour, c_clockoutmin, c_workinghour, c_attendstatus, c_worktype, c_tasktype FROM t_attendance WHERE c_empid = @empid ORDER BY c_attenddate",
                                _conn);
                            attCmd.Parameters.AddWithValue("@empid", EmployeeId);
                            
                            await _conn.OpenAsync();
                            using var attReader = await attCmd.ExecuteReaderAsync();
                            var attendances = new List<t_Attendance>();
                            
                            while (await attReader.ReadAsync())
                            {
                                attendances.Add(new t_Attendance
                                {
                                    AttendId = attReader.GetInt32(attReader.GetOrdinal("c_attendid")),
                                    EmpId = attReader.GetInt32(attReader.GetOrdinal("c_empid")),
                                    AttendDate = attReader.GetFieldValue<DateOnly>(attReader.GetOrdinal("c_attenddate")).ToDateTime(TimeOnly.MinValue),
                                    ClockInHour = attReader["c_clockinhour"] == DBNull.Value ? null : Convert.ToInt32(attReader["c_clockinhour"]),
                                    ClockInMin = attReader["c_clockinmin"] == DBNull.Value ? null : Convert.ToInt32(attReader["c_clockinmin"]),
                                    ClockOutHour = attReader["c_clockouthour"] == DBNull.Value ? null : Convert.ToInt32(attReader["c_clockouthour"]),
                                    ClockOutMin = attReader["c_clockoutmin"] == DBNull.Value ? null : Convert.ToInt32(attReader["c_clockoutmin"]),
                                    WorkingHour = attReader["c_workinghour"] == DBNull.Value ? null : Convert.ToInt32(attReader["c_workinghour"]),
                                    AttendStatus = attReader["c_attendstatus"]?.ToString(),
                                    WorkType = attReader["c_worktype"]?.ToString(),
                                    TaskType = attReader["c_tasktype"]?.ToString()
                                });
                            }
                            
                            await _conn.CloseAsync();
                            
                            if (attendances.Count > 0)
                            {
                                await _elasticSearchService.ReIndexAttendanceWithEmployeeDataAsync(
                                    attendances,
                                    updatedEmp.Name ?? "Unknown",
                                    updatedEmp.Email ?? "",
                                    updatedEmp.Status ?? "");
                            }
                        }
                    }
                    catch (Exception esEx)
                    {
                        Console.WriteLine("ES Re-indexing Error: " + esEx.Message);
                        // Don't fail update if ES fails
                    }
                }

                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateUser Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return false;
        }

        public async Task<bool> UpdateUserStatus(int employeeId, string status)
        {
            try
            {
                using var cmd = new NpgsqlCommand(
                    "UPDATE t_employee SET c_status = @status WHERE c_empid = @empid",
                    _conn);

                cmd.Parameters.AddWithValue("@empid", employeeId);
                cmd.Parameters.AddWithValue("@status", status);

                await _conn.OpenAsync();

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateUserStatus Error: " + ex.Message);
                return false;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
        public async Task<int> ChangePassword(vm_ChangePassword changePassword)
        {
            int result = 0;
            try
            {
                using var cmd = new NpgsqlCommand(
                    "UPDATE t_employee SET c_password = @password WHERE c_empid = @empid",
                    _conn);
                cmd.Parameters.AddWithValue("@empid", changePassword.EmployeeId);
                cmd.Parameters.AddWithValue("@password", changePassword.NewPassword);

                await _conn.OpenAsync();
                result = await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ChangePassword Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return result;
        }
    }
}
