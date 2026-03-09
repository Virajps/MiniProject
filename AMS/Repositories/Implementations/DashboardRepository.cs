using Npgsql;
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
                "SELECT COUNT(*) FROM t_employee", _conn))
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
        FROM t_attendance 
        WHERE c_attenddate = CURRENT_DATE", _conn))
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
        )", _conn))
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
        c_tasktype AS task,
        SUM(c_workinghour) AS hours
    FROM t_attendance
    WHERE c_tasktype IS NOT NULL
    GROUP BY c_tasktype", _conn))
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
                        Status = reader["c_attendstatus"].ToString()
                    });
                }

                reader.Close();
            }

            _conn.Close();

            return model;
        }
    }
}