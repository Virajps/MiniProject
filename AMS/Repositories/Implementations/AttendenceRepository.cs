using Npgsql;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public class AttendenceRepository : IAttendenceInterface
{
    private readonly NpgsqlConnection _conn;
    public AttendenceRepository(NpgsqlConnection conn)
    {
        _conn = conn;
    }
     public async Task<List<vm_TaskSummary>>GetEmployeeTaskSummary(int EmployeeId)
        {
            var list =new List<vm_TaskSummary>();

            try
            {
                await _conn.CloseAsync();

                using var cmd=new NpgsqlCommand(@"
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
                using var r=await cmd.ExecuteReaderAsync();
                while(await r.ReadAsync())
                {
                    list.Add(new vm_TaskSummary
                    {
                        TaskType = r["task"]?.ToString(),
                        TotalHours = Convert.ToInt32(r["hours"]?.ToString() ?? "0")
                    });
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("GetEmployeeTaskSummary Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return list;
        }
}
