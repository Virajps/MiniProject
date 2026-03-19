using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Repositories.Models;

namespace Repositories.Services
{
    public class DashboardElasticService
    {
        private readonly ElasticsearchClient _client;
        private readonly string _index;

        public DashboardElasticService(ElasticsearchClient client, IConfiguration config)
        {
            _client = client;
            _index = config["Elasticsearch:DefaultIndex"];
        }

        public async Task<DashboardModel> GetDashboardData()
        {
            var model = new DashboardModel
            {
                TaskHours = new List<TaskChartModel>(),
                RecentAttendance = new List<AttendanceModel>()
            };

            // 🔹 1. Total Employees
            var totalEmp = await _client.CountAsync<t_Attendance>(c => c
                .Index(_index)
                .Query(q => q.Term(t => t.Field("role.keyword").Value("Employee")))
            );
            model.TotalEmployees = (int)totalEmp.Count;

            // 🔹 2. Active Employees
            var activeEmp = await _client.CountAsync<t_Attendance>(c => c
                .Index(_index)
                .Query(q => q.Term(t => t.Field("status.keyword").Value("Active")))
            );
            model.ActiveEmployees = (int)activeEmp.Count;

            // 🔹 3. Present Today
            var today = DateTime.UtcNow.Date;

            var present = await _client.CountAsync<t_Attendance>(c => c
                .Index(_index)
                .Query(q => q.Bool(b => b.Must(
                    m => m.DateRange(r => r
                        .Field("attendDate")
                        .Gte(today)
                        .Lt(today.AddDays(1))
                    )
                )))
            );
            model.PresentToday = (int)present.Count;

            // 🔹 4. Late Today
            var late = await _client.CountAsync<t_Attendance>(c => c
                .Index(_index)
                .Query(q => q.Bool(b => b.Must(
                    m => m.Term(t => t.Field("attendstatus.keyword").Value("LateIn")),
                    m => m.DateRange(r => r
                        .Field("attendDate")
                        .Gte(today)
                        .Lt(today.AddDays(1))
                    )
                )))
            );
            model.LateToday = (int)late.Count;

            // 🔹 5. Absent = Total - Present
            model.AbsentToday = model.TotalEmployees - model.PresentToday;

            // 🔹 6. Task Aggregation (Pie Chart)
            var taskAgg = await _client.SearchAsync<t_Attendance>(s => s
                .Index(_index)
                .Size(0)
                .Aggregations(a => a
                    .Terms("tasks", t => t
                        .Field("taskType.keyword")
                        .Aggregations(aa => aa
                            .Sum("hours", sm => sm.Field("workingHour"))
                        )
                    )
                )
            );

            var buckets = taskAgg.Aggregations.Terms("tasks").Buckets;

            foreach (var b in buckets)
            {
                model.TaskHours.Add(new TaskChartModel
                {
                    Task = b.Key.ToString(),
                    Hours = (int)b.Sum("hours").Value
                });
            }

            // 🔹 7. Recent Attendance
            var recent = await _client.SearchAsync<t_Attendance>(s => s
                .Index(_index)
                .Size(10)
                .Sort(ss => ss.Descending("attendDate"))
            );

            foreach (var doc in recent.Documents)
            {
                model.RecentAttendance.Add(new AttendanceModel
                {
                    EmployeeName = doc.EmployeeName,
                    CheckIn = $"{doc.ClockInHour}:{doc.ClockInMin}",
                    CheckOut = $"{doc.ClockOutHour}:{doc.ClockOutMin}",
                    WorkingHour = doc.WorkingHour.ToString(),
                    Status = doc.AttendStatus
                });
            }

            return model;
        }
    }
}