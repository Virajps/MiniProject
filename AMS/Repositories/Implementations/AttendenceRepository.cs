using Npgsql;
using Repositories.Interfaces;
using Repositories.Models;


namespace Repositories.Implementations
{

    public class AttendenceRepository : IAttendenceInterface
    {
        private readonly NpgsqlConnection _conn;
        public AttendenceRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }
        public async Task<List<vm_TaskSummary>> GetEmployeeTaskSummary(int EmployeeId)
        {
            var list = new List<vm_TaskSummary>();

            try
            {
                await _conn.CloseAsync();

                using var cmd = new NpgsqlCommand(@"
                SELECT 
                    task,
                    SUM(c_workinghour/task_count) AS hours
                FROM(
                    SELECT 
                        UNNEST(STRING_TO_ARRAY(c_tasktype),',')AS task,
                        c_workinghour,
                        ARRAY_LENGTH(STRING_TO_ARRAY(c_tasktype,','),1) AS task_count
                    FROM t_attendace
                    WHERE c_empid=@empid
                    AND c_tasktype IS NOT NULL
                )sub
                GROUP BY task;", _conn);

                cmd.Parameters.AddWithValue("@empid", EmployeeId);
                await _conn.OpenAsync();
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new vm_TaskSummary
                    {
                        TaskType = r["task"]?.ToString(),
                        TotalHours = Convert.ToInt32(r["hours"]?.ToString() ?? "0")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetEmployeeTaskSummary Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return list;
        }

        public async Task<vm_AttendenceSummary> GetEmployeeAttendanceSummary(int employeeId)
        {
            var summary = new vm_AttendenceSummary();

            try
            {
                await _conn.CloseAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT 
                COUNT(*) AS present_days,
                COALESCE(SUM(c_workinghour),0) AS total_hours,
                COUNT(CASE WHEN c_attendstatus='LateIn' THEN 1 END) AS late_in,
                COUNT(CASE WHEN c_attendstatus='EarlyOut' THEN 1 END) AS early_out
            FROM t_attendance
            WHERE c_empid=@id;", _conn);

                cmd.Parameters.AddWithValue("@id", employeeId);

                await _conn.OpenAsync();

                using var r = await cmd.ExecuteReaderAsync();

                if (await r.ReadAsync())
                {
                    summary.PresentCount = Convert.ToInt32(r["present_days"]);
                    summary.TotalWorkingHours = Convert.ToInt32(r["total_hours"]);
                    summary.LateInCount = Convert.ToInt32(r["late_in"]);
                    summary.EarlyOutCount = Convert.ToInt32(r["early_out"]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return summary;
        }

        public async Task<t_Attendance> GetTodayAttendance(int empId)
        {
            t_Attendance? att = null;
            try
            {
                await _conn.CloseAsync();
                using var cmd = new NpgsqlCommand(
                    "SELECT * FROM t_attendance WHERE c_empid=@id AND c_attenddate=@today", _conn);
                cmd.Parameters.AddWithValue("@id", empId);
                cmd.Parameters.AddWithValue("@today", DateTime.Today);
                await _conn.OpenAsync();
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) att = MapRow(r);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            finally { await _conn.CloseAsync(); }
            return att;
        }

        private static t_Attendance MapRow(NpgsqlDataReader r)
        {
            return new t_Attendance
            {
                AttendId = r.GetInt32(r.GetOrdinal("c_attendid")),
                EmpId = r.GetInt32(r.GetOrdinal("c_empid")),
                AttendDate = r.GetDateTime(r.GetOrdinal("c_attenddate")),
                ClockInHour = r["c_clockinhour"] == DBNull.Value ? null : Convert.ToInt32(r["c_clockinhour"]),
                ClockInMin = r["c_clockinmin"] == DBNull.Value ? null : Convert.ToInt32(r["c_clockinmin"]),
                ClockOutHour = r["c_clockouthour"] == DBNull.Value ? null : Convert.ToInt32(r["c_clockouthour"]),
                ClockOutMin = r["c_clockoutmin"] == DBNull.Value ? null : Convert.ToInt32(r["c_clockoutmin"]),
                WorkingHour = r["c_workinghour"] == DBNull.Value ? null : Convert.ToInt32(r["c_workinghour"]),
                AttendStatus = r["c_attendstatus"]?.ToString(),
                WorkType = r["c_worktype"]?.ToString(),
                TaskType = r["c_tasktype"]?.ToString()
            };
        }

        public async Task<vm_AttendanceChartResult> GetAttendanceChart(int empId, string type, DateTime date)
        {
            var result = new vm_AttendanceChartResult();

            DateTime start;
            DateTime end;
            string query = "";

            if (type == "year")
            {
                start = new DateTime(date.Year, 1, 1);
                end = start.AddYears(1);

                query = @"
                            SELECT 
                                TO_CHAR(c_attenddate,'Mon') AS label,
                                SUM(c_workinghour) AS hours
                            FROM t_attendance
                            WHERE c_empid=@id
                            AND c_attenddate>=@start
                            AND c_attenddate<@end
                            GROUP BY EXTRACT(MONTH FROM c_attenddate),label
                            ORDER BY EXTRACT(MONTH FROM c_attenddate)";
            }

            else if (type == "month")
            {
                start = new DateTime(date.Year, date.Month, 1);
                end = start.AddMonths(1);

                query = @"
                    SELECT 
                        EXTRACT(DAY FROM c_attenddate) AS label,
                        SUM(c_workinghour) AS hours
                    FROM t_attendance
                    WHERE c_empid=@id
                    AND c_attenddate>=@start
                    AND c_attenddate<@end
                    GROUP BY label
                    ORDER BY label";
            }

            else
            {
                start = date.AddDays(-(int)date.DayOfWeek);
                end = start.AddDays(7);

                query = @"
                    SELECT 
                        TO_CHAR(c_attenddate,'Dy') AS label,
                        SUM(c_workinghour) AS hours
                    FROM t_attendance
                    WHERE c_empid=@id
                    AND c_attenddate>=@start
                    AND c_attenddate<@end
                    GROUP BY label,c_attenddate
                    ORDER BY c_attenddate";
            }

            try
            {
                await _conn.CloseAsync();

                using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@id", empId);
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);

                await _conn.OpenAsync();

                using var r = await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync())
                {
                    int hours = Convert.ToInt32(r["hours"]);

                    result.ChartData.Add(new vm_AttendanceChart
                    {
                        Label = r["label"].ToString(),
                        Hours = hours
                    });

                    result.TotalHours += hours;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return result;
        }
    }
}
