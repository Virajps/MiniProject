using Npgsql;
using Repositories.Interfaces;
using Repositories.Models;


namespace Repositories.Implementations
{

    public class AttendenceRepository : IAttendenceInterface
    {
        private readonly NpgsqlConnection _conn;
        private const int ClockInHourLimit = 9;
        private const int ClockInMinLimit = 15;
        private const int ClockOutHourLimit = 17;
        private const int ClockOutMinLimit = 0;
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
        SUM(c_workinghour::numeric / task_count) AS hours
    FROM (
        SELECT 
            UNNEST(STRING_TO_ARRAY(c_tasktype, ',')) AS task,
            c_workinghour,
            ARRAY_LENGTH(STRING_TO_ARRAY(c_tasktype, ','), 1) AS task_count
        FROM t_attendance
        WHERE c_empid = @empid
        AND c_tasktype IS NOT NULL
    ) sub
    GROUP BY task;
", _conn);

                cmd.Parameters.AddWithValue("@empid", EmployeeId);
                await _conn.OpenAsync();
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new vm_TaskSummary
                    {
                        TaskType = r["task"]?.ToString(),
                        TotalHours = Convert.ToInt32(Convert.ToDecimal(r["hours"]))
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
                cmd.Parameters.AddWithValue("@today", DateOnly.FromDateTime(DateTime.Today));
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
            var dateOnly = r.GetFieldValue<DateOnly>(r.GetOrdinal("c_attenddate"));

            return new t_Attendance
            {
                AttendId = r.GetInt32(r.GetOrdinal("c_attendid")),
                EmpId = r.GetInt32(r.GetOrdinal("c_empid")),
                AttendDate = dateOnly.ToDateTime(TimeOnly.MinValue),
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

        // --- Helpers ---
        private static bool IsLateIn(int hour, int minute)
            => hour > ClockInHourLimit || (hour == ClockInHourLimit && minute > ClockInMinLimit);

        private static bool IsEarlyOut(int hour, int minute)
            => hour < ClockOutHourLimit || (hour == ClockOutHourLimit && minute < ClockOutMinLimit);

        private static int CalculateWorkingHours(int inH, int inM, int outH, int outM)
        {
            var inTotal = inH * 60 + inM;
            var outTotal = outH * 60 + outM;
            return Math.Max(0, (outTotal - inTotal) / 60);
        }

        public async Task<int> ClockIn(int empId, string workType)
        {
            try
            {
                // Prevent double clock-in
                var existing = await GetTodayAttendance(empId);
                if (existing != null) return 0;

                var now = DateTime.Now;
                var status = IsLateIn(now.Hour, now.Minute) ? "LateIn" : "Regular";

                await _conn.CloseAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO t_attendance (c_empid, c_attenddate, c_clockinhour, c_clockinmin, c_attendstatus, c_worktype)
                      VALUES (@empid, @date, @hour, @min, @status, @wtype)", _conn);
                cmd.Parameters.AddWithValue("@empid", empId);
                cmd.Parameters.AddWithValue("@date", DateOnly.FromDateTime(DateTime.Today));
                cmd.Parameters.AddWithValue("@hour", now.Hour);
                cmd.Parameters.AddWithValue("@min", now.Minute);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@wtype", workType);
                await _conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                return 1;
            }
            catch (Exception ex) { Console.WriteLine("ClockIn Error: " + ex.Message); return -1; }
            finally { await _conn.CloseAsync(); }
        }

        public async Task<int> ClockOut(int empId, List<string> taskTypes)
        {
            try
            {
                var today = await GetTodayAttendance(empId);
                if (today == null || today.ClockInHour == null) return 0;
                if (today.ClockOutHour != null) return -2; // already clocked out

                var now = DateTime.Now;
                var taskJoined = string.Join(",", taskTypes);
                var workHours = CalculateWorkingHours(today.ClockInHour.Value, today.ClockInMin ?? 0, now.Hour, now.Minute);
                var status = IsEarlyOut(now.Hour, now.Minute) ? "EarlyOut"
                                : today.AttendStatus == "LateIn" ? "LateIn"
                                : "Regular";

                await _conn.CloseAsync();
                using var cmd = new NpgsqlCommand(
                    @"UPDATE t_attendance
                      SET c_clockouthour=@coh, c_clockoutmin=@com,
                          c_workinghour=@wh, c_attendstatus=@status, c_tasktype=@task
                      WHERE c_empid=@id AND c_attenddate=@today", _conn);
                cmd.Parameters.AddWithValue("@coh", now.Hour);
                cmd.Parameters.AddWithValue("@com", now.Minute);
                cmd.Parameters.AddWithValue("@wh", workHours);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@task", taskJoined);
                cmd.Parameters.AddWithValue("@id", empId);
                cmd.Parameters.AddWithValue("@today", DateOnly.FromDateTime(DateTime.Today));
                await _conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                return 1;
            }
            catch (Exception ex) { Console.WriteLine("ClockOut Error: " + ex.Message); return -1; }
            finally { await _conn.CloseAsync(); }
        }

        public async Task<List<vm_TaskSummary>> GetAllTaskSummary()
        {
            var list = new List<vm_TaskSummary>();
            try
            {
                await _conn.CloseAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT UNNEST(STRING_TO_ARRAY(c_tasktype,',')) AS task,
                             SUM(c_workinghour) AS hours
                      FROM t_attendance WHERE c_tasktype IS NOT NULL
                      GROUP BY task ORDER BY task", _conn);
                await _conn.OpenAsync();
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new vm_TaskSummary
                    {
                        TaskType = r["task"]?.ToString()?.Trim(),
                        TotalHours = Convert.ToInt32(r["hours"])
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            finally { await _conn.CloseAsync(); }
            return list;
        }

        public async Task<List<vm_AttendanceScheduler>> GetAttendanceScheduler(int empId)
        {
            var list = new List<vm_AttendanceScheduler>();

            try
            {
                await _conn.CloseAsync();

                using var cmd = new NpgsqlCommand(
                @"SELECT * FROM t_attendance
          WHERE c_empid=@id
          ORDER BY c_attenddate", _conn);

                cmd.Parameters.AddWithValue("@id", empId);

                await _conn.OpenAsync();

                using var r = await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync())
                {
                    DateOnly dateOnly = r.GetFieldValue<DateOnly>(r.GetOrdinal("c_attenddate"));
                    DateTime date = dateOnly.ToDateTime(TimeOnly.MinValue);

                    int inH = r["c_clockinhour"] == DBNull.Value ? 9 : Convert.ToInt32(r["c_clockinhour"]);
                    int inM = r["c_clockinmin"] == DBNull.Value ? 0 : Convert.ToInt32(r["c_clockinmin"]);

                    int outH = r["c_clockouthour"] == DBNull.Value ? 17 : Convert.ToInt32(r["c_clockouthour"]);
                    int outM = r["c_clockoutmin"] == DBNull.Value ? 0 : Convert.ToInt32(r["c_clockoutmin"]);

                    list.Add(new vm_AttendanceScheduler
                    {
                        Id = Convert.ToInt32(r["c_attendid"]),
                        Title = "Attendance",
                        Start = new DateTime(date.Year, date.Month, date.Day, inH, inM, 0),
                        End = new DateTime(date.Year, date.Month, date.Day, outH, outM, 0),
                        Status = r["c_attendstatus"]?.ToString(),
                        WorkType = r["c_worktype"]?.ToString(),
                        TaskType = r["c_tasktype"]?.ToString(),
                        WorkingHour = r["c_workinghour"] == DBNull.Value ? 0 : Convert.ToInt32(r["c_workinghour"])
                    });
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

            return list;
        }
    }
}
