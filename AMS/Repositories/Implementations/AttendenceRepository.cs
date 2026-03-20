using Npgsql;
using Repositories.Interfaces;
using Repositories.Models;


namespace Repositories.Implementations
{

    public class AttendenceRepository : IAttendenceInterface
    {
        private readonly NpgsqlConnection _conn;
        private readonly IAttedanceCacheService _attendanceCacheService;
        private readonly IEmployeeInterface _employee;
        private const int ClockInHourLimit = 9;
        private const int ClockInMinLimit = 15;
        private const int ClockOutHourLimit = 17;
        private const int ClockOutMinLimit = 0;
        public AttendenceRepository(NpgsqlConnection conn, IAttedanceCacheService attedanceCacheService,IEmployeeInterface employee)
        {
            _conn = conn;
            _attendanceCacheService = attedanceCacheService;
            _employee=employee;
        }
        public async Task<List<vm_TaskSummary>> GetEmployeeTaskSummary(int EmployeeId, string type, DateTime date)
        {
            var list = new List<vm_TaskSummary>();
            DateTime start;
            DateTime end;

            if (type == "year")
            {
                start = new DateTime(date.Year, 1, 1);
                end = start.AddYears(1);
            }
            else if (type == "month")
            {
                start = new DateTime(date.Year, date.Month, 1);
                end = start.AddMonths(1);
            }
            else
            {
                start = date.AddDays(-(int)date.DayOfWeek);
                end = start.AddDays(7);
            }

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
        AND c_attenddate >= @start
        AND c_attenddate < @end
    ) sub
    GROUP BY task;
", _conn);

                cmd.Parameters.AddWithValue("@empid", EmployeeId);
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);
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

        public async Task<List<t_Attendance>> GetAllAttendance()
        {
            var list = new List<t_Attendance>();

            try
            {
                await _conn.CloseAsync();

                using var cmd = new NpgsqlCommand(
                @"SELECT a.*, e.c_name FROM t_attendance a
                LEFT JOIN t_employee e 
                ON a.c_empid = e.c_empid
                ORDER BY a.c_attenddate", _conn);

                await _conn.OpenAsync();

                using var r = await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync())
                {
                    list.Add(MapRow(r));
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

        public async Task<t_Attendance> GetTodayAttendance(int empId)
        {
            t_Attendance? att = null;
            try
            {
                await _conn.CloseAsync();
                using var cmd = new NpgsqlCommand(
                        @"SELECT a.*, e.c_name 
                        FROM t_attendance a
                        LEFT JOIN t_employee e 
                        ON a.c_empid = e.c_empid
                        WHERE a.c_empid = @id 
                        AND a.c_attenddate = @today", _conn);
                cmd.Parameters.AddWithValue("@id", empId);
                cmd.Parameters.AddWithValue("@today", DateOnly.FromDateTime(DateTime.Today));
                await _conn.OpenAsync();
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) att = MapRow(r);
            }
            catch (Exception ex) { 
                Console.WriteLine("get atten"+ex.Message);
            }
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
                TaskType = r["c_tasktype"]?.ToString(),
                EmployeeName = r["c_name"]?.ToString()
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

                await r.CloseAsync();

                using var summaryCmd = new NpgsqlCommand(@"
                    SELECT
                        COUNT(CASE WHEN c_attendstatus='LateIn' THEN 1 END) AS late_in,
                        COUNT(CASE WHEN c_attendstatus='EarlyOut' THEN 1 END) AS early_out
                    FROM t_attendance
                    WHERE c_empid=@id
                    AND c_attenddate>=@start
                    AND c_attenddate<@end;", _conn);

                summaryCmd.Parameters.AddWithValue("@id", empId);
                summaryCmd.Parameters.AddWithValue("@start", start);
                summaryCmd.Parameters.AddWithValue("@end", end);

                using var summaryReader = await summaryCmd.ExecuteReaderAsync();
                if (await summaryReader.ReadAsync())
                {
                    result.LateInCount = Convert.ToInt32(summaryReader["late_in"]);
                    result.EarlyOutCount = Convert.ToInt32(summaryReader["early_out"]);
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
                var existing = await GetTodayAttendance(empId);
                if (existing != null) return 0;

                var cachedClockIn = await _attendanceCacheService.GetClockInAsync(empId);
                if (cachedClockIn != null && cachedClockIn.ClockInTime.Date == DateTime.Today) return 0;

                var now = DateTime.Now;
                var status = IsLateIn(now.Hour, now.Minute) ? "LateIn" : "Regular";

                var data = await _employee.GetUserById(empId);
                var ename = data.Name;

                await _attendanceCacheService.SetClockInAsync(empId,ename, now, workType, status);
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
                if (today != null)
                {
                    return today.ClockOutHour != null ? -2 : 0;
                }

                var cachedClockIn = await _attendanceCacheService.GetClockInAsync(empId);
                if (cachedClockIn == null || cachedClockIn.ClockInTime.Date != DateTime.Today) return 0;

                var now = DateTime.Now;
                var taskJoined = string.Join(",", taskTypes);
                var workHours = CalculateWorkingHours(cachedClockIn.ClockInTime.Hour, cachedClockIn.ClockInTime.Minute, now.Hour, now.Minute);
                var status = IsEarlyOut(now.Hour, now.Minute) ? "EarlyOut"
                                : cachedClockIn.Status == "LateIn" ? "LateIn"
                                : "Regular";

                await _conn.CloseAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO t_attendance
                      (c_empid, c_attenddate, c_clockinhour, c_clockinmin, c_clockouthour, c_clockoutmin, c_workinghour, c_attendstatus, c_worktype, c_tasktype)
                      VALUES
                      (@id, @today, @cih, @cim, @coh, @com, @wh, @status, @wtype, @task)", _conn);
                cmd.Parameters.AddWithValue("@id", empId);
                cmd.Parameters.AddWithValue("@today", DateOnly.FromDateTime(DateTime.Today));
                cmd.Parameters.AddWithValue("@cih", cachedClockIn.ClockInTime.Hour);
                cmd.Parameters.AddWithValue("@cim", cachedClockIn.ClockInTime.Minute);
                cmd.Parameters.AddWithValue("@coh", now.Hour);
                cmd.Parameters.AddWithValue("@com", now.Minute);
                cmd.Parameters.AddWithValue("@wh", workHours);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@wtype", cachedClockIn.WorkType);
                cmd.Parameters.AddWithValue("@task", taskJoined);
                await _conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                await _attendanceCacheService.RemoveClockInAsync(empId);
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
            var attendanceMap = new Dictionary<DateTime, vm_AttendanceScheduler>();

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

                    var attendance = new vm_AttendanceScheduler
                    {
                        Id = Convert.ToInt32(r["c_attendid"]),
                        Title = "Attendance",
                        Start = new DateTime(date.Year, date.Month, date.Day, inH, inM, 0),
                        End = new DateTime(date.Year, date.Month, date.Day, outH, outM, 0),
                        Status = r["c_attendstatus"]?.ToString(),
                        WorkType = r["c_worktype"]?.ToString(),
                        TaskType = r["c_tasktype"]?.ToString(),
                        WorkingHour = r["c_workinghour"] == DBNull.Value ? 0 : Convert.ToInt32(r["c_workinghour"])
                    };

                    attendanceMap[date.Date] = attendance;
                }

                await r.CloseAsync();

                // Determine current month range
                DateTime startMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime endMonth = startMonth.AddMonths(1).AddDays(-1);

                int idCounter = 100000;

                for (DateTime d = startMonth; d <= endMonth; d = d.AddDays(1))
                {
                    // Skip Sunday (Holiday)
                    if (d.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    if (attendanceMap.ContainsKey(d))
                    {
                        list.Add(attendanceMap[d]);
                    }
                    else
                    {
                        if (d.Date <= DateTime.Today)
                        {
                            list.Add(new vm_AttendanceScheduler
                            {
                                Id = idCounter++,
                                Title = "Attendance",
                                Start = d,
                                End = d,
                                Status = "Absent",
                                WorkingHour = 0
                            });
                        }
                    }
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

        public async Task<List<vm_AttendanceScheduler>> GetAttendanceScheduler1(int empId)
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
