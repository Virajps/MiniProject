using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Repositories.Models;

namespace Repositories.Services
{
    public class ElasticSearchService
    {
        private const int MaxSearchSize = 10000;
        private readonly ElasticsearchClient _client;
        private readonly string _indexName;

        public ElasticSearchService(IConfiguration configuration, ElasticsearchClient client)
        {
            _client = client;
            _indexName = configuration["Elasticsearch:DefaultIndex"] ?? "attendance";
        }

        public async Task<int> CreateIndexAsync()
        {
            try
            {
                var existsResponse = await _client.Indices.ExistsAsync(_indexName);
                if (existsResponse.Exists)
                {
                    return 0;
                }

                var createResponse = await _client.Indices.CreateAsync(_indexName);
                return createResponse.IsValidResponse ? 1 : -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateIndexAsync Error: " + ex.Message);
                return -1;
            }
        }

        public async Task<bool> IndexAttendanceAsync(
            t_Attendance attendance,
            string? employeeName = null,
            string? employeeEmail = null,
            string? employeeStatus = null)
        {
            try
            {
                await EnsureIndexExistsAsync();

                var document = AttendanceElasticDocument.FromAttendance(
                    attendance,
                    employeeName,
                    employeeEmail,
                    employeeStatus);

                var response = await _client.IndexAsync(document, idx => idx
                    .Index(_indexName)
                    .Id(document.AttendId));

                return response.IsValidResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine("IndexAttendanceAsync Error: " + ex.Message);
                return false;
            }
        }

        public async Task<int> BulkIndexAttendanceAsync(
            IEnumerable<t_Attendance> attendances,
            Func<int, Task<EmployeeElasticInfo?>>? employeeResolver = null)
        {
            var indexedCount = 0;

            foreach (var attendance in attendances)
            {
                EmployeeElasticInfo? employeeInfo = null;

                if (employeeResolver != null)
                {
                    employeeInfo = await employeeResolver(attendance.EmpId);
                }

                var indexed = await IndexAttendanceAsync(
                    attendance,
                    employeeInfo?.EmployeeName,
                    employeeInfo?.EmployeeEmail,
                    employeeInfo?.EmployeeStatus);

                if (indexed)
                {
                    indexedCount++;
                }
            }

            return indexedCount;
        }

        public async Task<bool> DeleteAttendanceAsync(int attendId)
        {
            try
            {
                var response = await _client.DeleteAsync<AttendanceElasticDocument>(
                    attendId,
                    d => d.Index(_indexName));

                return response.IsValidResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine("DeleteAttendanceAsync Error: " + ex.Message);
                return false;
            }
        }

        public async Task<List<vm_TaskSummary>> GetEmployeeTaskSummaryAsync(int employeeId, string type, DateTime date)
        {
            var (start, end) = ResolveDateRange(type, date);
            var documents = await SearchAttendanceAsync(employeeId, start, end);

            return documents
                .SelectMany(GetTaskHourAllocations)
                .GroupBy(x => x.TaskType, StringComparer.OrdinalIgnoreCase)
                .Select(g => new vm_TaskSummary
                {
                    TaskType = g.Key,
                    TotalHours = Convert.ToInt32(g.Sum(x => x.Hours))
                })
                .OrderBy(x => x.TaskType)
                .ToList();
        }

        public async Task<vm_AttendanceChartResult> GetAttendanceChartAsync(int empId, string type, DateTime date)
        {
            var (start, end) = ResolveDateRange(type, date);
            var documents = await SearchAttendanceAsync(empId, start, end);
            var result = new vm_AttendanceChartResult
            {
                LateInCount = documents.Count(x => string.Equals(x.AttendStatus, "LateIn", StringComparison.OrdinalIgnoreCase)),
                EarlyOutCount = documents.Count(x => string.Equals(x.AttendStatus, "EarlyOut", StringComparison.OrdinalIgnoreCase))
            };

            IEnumerable<IGrouping<string, AttendanceElasticDocument>> groups;

            if (string.Equals(type, "year", StringComparison.OrdinalIgnoreCase))
            {
                groups = documents
                    .GroupBy(x => x.AttendDate.ToString("MMM"))
                    .OrderBy(x => DateTime.ParseExact(x.Key, "MMM", null).Month);
            }
            else if (string.Equals(type, "month", StringComparison.OrdinalIgnoreCase))
            {
                groups = documents
                    .GroupBy(x => x.AttendDate.Day.ToString())
                    .OrderBy(x => int.Parse(x.Key));
            }
            else
            {
                groups = documents
                    .OrderBy(x => x.AttendDate)
                    .GroupBy(x => x.AttendDate.ToString("ddd"));
            }

            foreach (var group in groups)
            {
                var hours = group.Sum(x => x.WorkingHour);
                result.ChartData.Add(new vm_AttendanceChart
                {
                    Label = group.Key,
                    Hours = hours
                });
                result.TotalHours += hours;
            }

            return result;
        }

        public async Task<vm_EmployeeProgressReport> GetMonthlyReportAsync(int empId, int month, int year)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            var documents = await SearchAttendanceAsync(empId, start, end);

            var report = new vm_EmployeeProgressReport
            {
                EmployeeId = empId,
                EmployeeName = documents.FirstOrDefault()?.EmployeeName,
                Present = documents.Count(x => string.Equals(x.AttendStatus, "Regular", StringComparison.OrdinalIgnoreCase)),
                LateIn = documents.Count(x => string.Equals(x.AttendStatus, "LateIn", StringComparison.OrdinalIgnoreCase)),
                EarlyOut = documents.Count(x => string.Equals(x.AttendStatus, "EarlyOut", StringComparison.OrdinalIgnoreCase)),
                TotalWorkingHours = documents.Sum(x => x.WorkingHour)
            };

            var distinctAttendanceDays = documents
                .Select(x => x.AttendDate.Date)
                .Distinct()
                .Count();

            report.Absent = DateTime.DaysInMonth(year, month) - distinctAttendanceDays;
            report.TotalAttendance = report.Present + report.LateIn + report.EarlyOut;

            report.Tasks = documents
                .SelectMany(GetTaskHourAllocations)
                .GroupBy(x => x.TaskType, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TaskSummary
                {
                    TaskType = g.Key,
                    Hours = Convert.ToInt32(g.Sum(x => x.Hours))
                })
                .OrderByDescending(x => x.Hours)
                .ThenBy(x => x.TaskType)
                .ToList();

            return report;
        }

        public async Task<int> GetTotalWorkingHoursAsync(int empId)
        {
            var documents = await SearchAttendanceByDateAsync(null, null, null, null);
            return documents
                .Where(x => x.EmpId == empId)
                .Sum(x => x.WorkingHour);
        }

        public async Task<List<EmployeeFilterResult>> FilterEmployeesAsync(EmployeeFilterRequest request)
        {
            var documents = await SearchAttendanceByDateAsync(request.FromDate, request.ToDate, request.Month, request.Year);

            if (request.EmployeeId.HasValue)
            {
                documents = documents.Where(x => x.EmpId == request.EmployeeId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.EmployeeName))
            {
                documents = documents.Where(x =>
                    !string.IsNullOrWhiteSpace(x.EmployeeName) &&
                    x.EmployeeName.Contains(request.EmployeeName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.EmployeeStatus))
            {
                documents = documents.Where(x =>
                    string.Equals(x.EmployeeStatus, request.EmployeeStatus, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.WorkType))
            {
                documents = documents.Where(x =>
                    string.Equals(x.WorkType, request.WorkType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.TaskType))
            {
                documents = documents.Where(x =>
                    x.TaskTypes.Any(t => string.Equals(t, request.TaskType, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.AttendStatus))
            {
                documents = documents.Where(x =>
                    string.Equals(x.AttendStatus, request.AttendStatus, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return documents
                .GroupBy(x => new { x.EmpId, x.EmployeeName, x.EmployeeEmail, x.EmployeeStatus })
                .Select(g => new EmployeeFilterResult
                {
                    EmployeeId = g.Key.EmpId,
                    EmployeeName = g.Key.EmployeeName,
                    Email = g.Key.EmployeeEmail,
                    EmployeeStatus = g.Key.EmployeeStatus,
                    TotalAttendance = g.Select(x => x.AttendDate.Date).Distinct().Count(),
                    TotalWorkingHours = g.Sum(x => x.WorkingHour),
                    LateInCount = g.Count(x => string.Equals(x.AttendStatus, "LateIn", StringComparison.OrdinalIgnoreCase)),
                    EarlyOutCount = g.Count(x => string.Equals(x.AttendStatus, "EarlyOut", StringComparison.OrdinalIgnoreCase)),
                    WorkTypes = g.Select(x => x.WorkType)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()!,
                    TaskTypes = g.SelectMany(x => x.TaskTypes)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .OrderBy(x => x.EmployeeName)
                .ThenBy(x => x.EmployeeId)
                .ToList();
        }

        public async Task<List<AnalysisBucketResult>> GetWorkTypeAnalysisAsync(AttendanceAnalysisRequest request)
        {
            var documents = await SearchAttendanceByDateAsync(request.FromDate, request.ToDate, request.Month, request.Year);

            if (request.EmployeeId.HasValue)
            {
                documents = documents.Where(x => x.EmpId == request.EmployeeId.Value).ToList();
            }

            return documents
                .GroupBy(x => string.IsNullOrWhiteSpace(x.WorkType) ? "Unknown" : x.WorkType!)
                .Select(g => new AnalysisBucketResult
                {
                    Name = g.Key,
                    RecordCount = g.Count(),
                    EmployeeCount = g.Select(x => x.EmpId).Distinct().Count(),
                    TotalWorkingHours = g.Sum(x => x.WorkingHour),
                    AverageWorkingHours = g.Any() ? Math.Round(g.Average(x => x.WorkingHour), 2) : 0
                })
                .OrderByDescending(x => x.TotalWorkingHours)
                .ThenBy(x => x.Name)
                .ToList();
        }

        public async Task<List<AnalysisBucketResult>> GetTaskTypeAnalysisAsync(AttendanceAnalysisRequest request)
        {
            var documents = await SearchAttendanceByDateAsync(request.FromDate, request.ToDate, request.Month, request.Year);

            if (request.EmployeeId.HasValue)
            {
                documents = documents.Where(x => x.EmpId == request.EmployeeId.Value).ToList();
            }

            return documents
                .SelectMany(GetTaskHourAllocations, (document, task) => new
                {
                    document.EmpId,
                    task.TaskType,
                    task.Hours
                })
                .GroupBy(x => x.TaskType, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AnalysisBucketResult
                {
                    Name = g.Key,
                    RecordCount = g.Count(),
                    EmployeeCount = g.Select(x => x.EmpId).Distinct().Count(),
                    TotalWorkingHours = Convert.ToInt32(g.Sum(x => x.Hours)),
                    AverageWorkingHours = g.Any() ? Convert.ToDouble(Math.Round(g.Average(x => x.Hours), 2)) : 0
                })
                .OrderByDescending(x => x.TotalWorkingHours)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private async Task EnsureIndexExistsAsync()
        {
            var existsResponse = await _client.Indices.ExistsAsync(_indexName);
            if (!existsResponse.Exists)
            {
                await _client.Indices.CreateAsync(_indexName);
            }
        }

        private async Task<List<AttendanceElasticDocument>> SearchAttendanceAsync(int empId, DateTime start, DateTime end)
        {
            try
            {
                var response = await _client.SearchAsync<AttendanceElasticDocument>(s => s
                    .Indices(_indexName)
                    .Size(MaxSearchSize));

                return response.Documents
                    .Where(x => x.EmpId == empId && x.AttendDate >= start && x.AttendDate < end)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("SearchAttendanceAsync Error: " + ex.Message);
                return new List<AttendanceElasticDocument>();
            }
        }

        private async Task<List<AttendanceElasticDocument>> SearchAttendanceByDateAsync(
            DateTime? fromDate,
            DateTime? toDate,
            int? month,
            int? year)
        {
            try
            {
                var response = await _client.SearchAsync<AttendanceElasticDocument>(s => s
                    .Indices(_indexName)
                    .Size(MaxSearchSize));

                var documents = response.Documents.ToList();

                if (month.HasValue && year.HasValue)
                {
                    var start = new DateTime(year.Value, month.Value, 1);
                    var end = start.AddMonths(1);
                    documents = documents
                        .Where(x => x.AttendDate >= start && x.AttendDate < end)
                        .ToList();
                }
                else
                {
                    if (fromDate.HasValue)
                    {
                        documents = documents
                            .Where(x => x.AttendDate.Date >= fromDate.Value.Date)
                            .ToList();
                    }

                    if (toDate.HasValue)
                    {
                        documents = documents
                            .Where(x => x.AttendDate.Date <= toDate.Value.Date)
                            .ToList();
                    }
                }

                return documents;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SearchAttendanceByDateAsync Error: " + ex.Message);
                return new List<AttendanceElasticDocument>();
            }
        }

        private static (DateTime Start, DateTime End) ResolveDateRange(string type, DateTime date)
        {
            if (string.Equals(type, "year", StringComparison.OrdinalIgnoreCase))
            {
                var start = new DateTime(date.Year, 1, 1);
                return (start, start.AddYears(1));
            }

            if (string.Equals(type, "month", StringComparison.OrdinalIgnoreCase))
            {
                var start = new DateTime(date.Year, date.Month, 1);
                return (start, start.AddMonths(1));
            }

            var weekStart = date.AddDays(-(int)date.DayOfWeek).Date;
            return (weekStart, weekStart.AddDays(7));
        }

        private static IEnumerable<TaskHourAllocation> GetTaskHourAllocations(AttendanceElasticDocument document)
        {
            if (document.TaskTypes.Count == 0)
            {
                yield break;
            }

            var hoursPerTask = document.WorkingHour <= 0
                ? 0
                : (decimal)document.WorkingHour / document.TaskTypes.Count;

            foreach (var task in document.TaskTypes)
            {
                if (string.IsNullOrWhiteSpace(task))
                {
                    continue;
                }

                yield return new TaskHourAllocation
                {
                    TaskType = task.Trim(),
                    Hours = hoursPerTask
                };
            }
        }
    }
}
