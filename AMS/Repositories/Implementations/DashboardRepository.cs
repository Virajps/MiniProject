using Npgsql;
using Repositories.Models;
namespace Repositories.Implementations
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly NpgsqlConnection _conn;
        public DashboardRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }
        public DashboardModel GetDashboardData()
        {
            DashboardModel model = new DashboardModel();
            model.TaskHours = new List<TaskChartModel>();
            model.RecentAttendance = new List<AttendanceModel>();

            _conn.Open();

            // 1️⃣ Total Employees
            using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM t_employee WHERE c_role='Employee'", _conn))
            {
                model.TotalEmployees = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // 2️⃣ Active Employees
            using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM t_employee WHERE c_status='Active'", _conn))
            {
                model.ActiveEmployees = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // 3️⃣ Present Today
            using (var cmd = new NpgsqlCommand(@"
        SELECT COUNT(*) 
        FROM t_attendance JOIN t_employee ON t_attendance.c_empid = t_employee.c_empid
        WHERE c_attenddate = CURRENT_DATE AND t_employee.c_role = 'Employee'", _conn))
            {
                model.PresentToday = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // 4️⃣ Absent Today
            using (var cmd = new NpgsqlCommand(@"
        SELECT COUNT(*) 
        FROM t_employee 
        WHERE c_empid NOT IN
        (
            SELECT c_empid 
            FROM t_attendance 
            WHERE c_attenddate = CURRENT_DATE
        ) AND c_role = 'Employee'", _conn))
            {
                model.AbsentToday = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // 5️⃣ Late Today
            using (var cmd = new NpgsqlCommand(@"
        SELECT COUNT(*) 
        FROM t_attendance 
        WHERE c_attenddate = CURRENT_DATE 
        AND c_attendstatus='LateIn'", _conn))
            {
                model.LateToday = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // 6️⃣ Task Working Hours (Pie Chart)
            using (var cmd = new NpgsqlCommand(@"
                SELECT 
                    task,
                    SUM(c_workinghour) AS hours
                FROM (
                    SELECT 
                        UNNEST(STRING_TO_ARRAY(c_tasktype, ',')) AS task,
                        c_workinghour
                    FROM t_attendance
                    WHERE c_tasktype IS NOT NULL
                ) t
                GROUP BY task", _conn))
                {
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        model.TaskHours.Add(new TaskChartModel
                        {
                            Task = reader["task"].ToString(),
                            Hours = Convert.ToInt32(reader["hours"])
                        });
                    }

                    reader.Close();
                }

            // 7️⃣ Recent Attendance
            using (var cmd = new NpgsqlCommand(@"
        SELECT 
            e.c_name,
            a.c_clockinhour,
            a.c_clockinmin,
            a.c_workinghour,
            a.c_clockouthour,
            a.c_clockoutmin,
            a.c_attendstatus
        FROM t_attendance a
        JOIN t_employee e ON e.c_empid = a.c_empid
        ORDER BY a.c_attenddate DESC
        LIMIT 10", _conn))
            {
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string checkin = reader["c_clockinhour"] + ":" + reader["c_clockinmin"];
                    string checkout = reader["c_clockouthour"] + ":" + reader["c_clockoutmin"];

                    model.RecentAttendance.Add(new AttendanceModel
                    {
                        EmployeeName = reader["c_name"].ToString(),
                        CheckIn = checkin,
                        CheckOut = checkout,
                        WorkingHour = reader["c_workinghour"].ToString(),
                        Status = reader["c_attendstatus"].ToString()
                    });
                }

                reader.Close();
            }

            _conn.Close();

            return model;
        }

        public async Task<List<AccessModel>> GetAllUsersForAccess()
        {
            var employees = new List<AccessModel>();
            try
            {
                using var cmd = new NpgsqlCommand(
                    @"SELECT e.c_empid, e.c_name, e.c_email, e.c_role, e.c_status, SUM(a.c_workinghour) as TotalHour 
                    FROM t_employee e
                    JOIN t_attendance a
                    ON e.c_empid = a.c_empid
                    WHERE c_role = 'Employee' 
                    GROUP BY e.c_empid, e.c_name, e.c_email, e.c_role, e.c_status 
                    ORDER BY c_empid",
                    _conn);

                await _conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    employees.Add(new AccessModel
                    {
                        EmployeeId = reader.GetInt32(reader.GetOrdinal("c_empid")),
                        Name = reader["c_name"]?.ToString(),
                        Email = reader["c_email"]?.ToString(),
                        Status = reader["c_status"]?.ToString(),
                        TotalHour = reader["TotalHour"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Accsess Control Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return employees;
        }
    }
}