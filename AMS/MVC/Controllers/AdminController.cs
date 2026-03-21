using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Services;

namespace MyApp.Namespace
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class AdminController : Controller
    {
        private readonly IAttendenceInterface _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IEmployeeInterface _employee;
        private readonly IGmailSmtpSenderInterface _email;
        private readonly ReportEmailService _reportEmailService;
        private readonly IDashboardRepository _dashboardRepository;
        private readonly ElasticSearchService _elasticSearchService;
        private readonly IRabbitRegistration _rabbitRegistration;

        public AdminController(
            IWebHostEnvironment env,
            IEmployeeInterface employee,
            IAttendenceInterface repo,
            IDashboardRepository dashboardRepository,
            IGmailSmtpSenderInterface email,
            ElasticSearchService elasticSearchService,
            IRabbitRegistration rabbitRegistration,
            ReportEmailService reportEmailService)
        {
            _env = env;
            _employee = employee;
            _repo = repo;
            _dashboardRepository = dashboardRepository;
            _email = email;
            _elasticSearchService = elasticSearchService;
            _rabbitRegistration=rabbitRegistration;
            _reportEmailService = reportEmailService;
        }
        // GET: AdminController
        public ActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                var data = _dashboardRepository.GetDashboardData();
                return View(data);
            }
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceScheduler(int empId)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                var data = await _repo.GetAttendanceScheduler(empId);
                return Json(data);
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateUserStatus(int EmployeeId, string Status)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                System.Console.WriteLine(EmployeeId + "" + Status);
                var result = await _employee.UpdateUserStatus(EmployeeId, Status);
                System.Console.WriteLine(result);

                if (result)
                {
                    var data = await _employee.GetUserById(EmployeeId);
                    var email = data.Email;
                    var name = data.Name;
                    bool active;
                    if (Status == "Active")
                    {
                        active = true;
                    }
                    else
                    {
                        active = false;
                    }
                    await _email.SendStatusEmail(email, name, active);

                    return Ok(new { success = true, message = "Status updated successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to update status" });
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> AccessControl()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> AccessControlData()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                var result = await _dashboardRepository.GetAllUsersForAccess();
                return Ok(new { success = true, data = result });
            }
        }

        [HttpGet]
        public IActionResult ProgressReport()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeProgress(int empId, int month, int year)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }
            else
            {
                try
                {
                    var data = await _dashboardRepository.GetEmployeeProgress(empId, month, year);
                    
                    if (data == null)
                    {
                        return Json(new { success = false, data = (object)null, message = "No data found" });
                    }
                    
                    return Json(new { success = true, data = data });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, data = (object)null, message = ex.Message });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendProgressReportEmail([FromBody] ProgressReportEmailRequest request)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return Unauthorized(new { success = false, message = "Unauthorized" });
            }

            if (request == null || request.EmpId <= 0 || string.IsNullOrWhiteSpace(request.PdfBase64))
            {
                return BadRequest(new { success = false, message = "Invalid report request" });
            }

            var empData = await _employee.GetUserById(request.EmpId);
            if (empData == null || string.IsNullOrWhiteSpace(empData.Email))
            {
                return BadRequest(new { success = false, message = "Employee email not found" });
            }

            byte[] pdfBytes;
            try
            {
                pdfBytes = Convert.FromBase64String(request.PdfBase64);
            }
            catch
            {
                return BadRequest(new { success = false, message = "Invalid PDF data" });
            }

            var fileName = string.IsNullOrWhiteSpace(request.FileName)
                ? $"report_{request.EmpId}_{request.Month}_{request.Year}.pdf"
                : request.FileName;

            await _reportEmailService.SendProgressReportEmail(empData.Email, empData.Name, pdfBytes, fileName);

            return Ok(new { success = true, message = "Report email sent successfully" });
        }

        public class ProgressReportEmailRequest
        {
            public int EmpId { get; set; }
            public int Month { get; set; }
            public int Year { get; set; }
            public string? FileName { get; set; }
            public string? PdfBase64 { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> FilterEmployees(
            int? employeeId,
            string? employeeName,
            string? employeeStatus,
            string? workType,
            string? taskType,
            string? attendStatus,
            int? month,
            int? year,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }

            var data = await _elasticSearchService.FilterEmployeesAsync(new EmployeeFilterRequest
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                EmployeeStatus = employeeStatus,
                WorkType = workType,
                TaskType = taskType,
                AttendStatus = attendStatus,
                Month = month,
                Year = year,
                FromDate = fromDate,
                ToDate = toDate
            });

            return Ok(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkTypeAnalysis(int? employeeId, int? month, int? year, DateTime? fromDate, DateTime? toDate)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }

            var data = await _elasticSearchService.GetWorkTypeAnalysisAsync(new AttendanceAnalysisRequest
            {
                EmployeeId = employeeId,
                Month = month,
                Year = year,
                FromDate = fromDate,
                ToDate = toDate
            });

            return Ok(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskTypeAnalysis(int? employeeId, int? month, int? year, DateTime? fromDate, DateTime? toDate)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }

            var data = await _elasticSearchService.GetTaskTypeAnalysisAsync(new AttendanceAnalysisRequest
            {
                EmployeeId = employeeId,
                Month = month,
                Year = year,
                FromDate = fromDate,
                ToDate = toDate
            });

            return Ok(new { success = true, data });
        }

//---------------Get Queue Messages----------------------//
        [HttpGet]
        public async Task<IActionResult> GetQueueMessages()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }

            var registrationMessages = await _rabbitRegistration.GetRegistrationNotificationsAsync();
            var attendanceMessages = await _rabbitRegistration.GetAttendanceNotificationsAsync();

            return Ok(new
            {
                success = true,
                registrationMessages,
                attendanceMessages
            });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveQueueMessage(string notificationId)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Unauthorized", "User");
            }

            var removed = await _rabbitRegistration.RemoveNotificationAsync(notificationId);
            return Ok(new { success = removed });
        }

        //------------- Kendo Grid Methods - ElasticSearch Data Binding ---------//

        [HttpGet]
        public async Task<IActionResult> GetEmployeeGridData(
            int? employeeId,
            string? employeeName,
            string? employeeStatus,
            string? workType,
            string? taskType,
            string? attendStatus,
            int? month,
            int? year,
            DateTime? fromDate,
            DateTime? toDate,
            int skip = 0,
            int take = 10)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                var allData = await _elasticSearchService.FilterEmployeesAsync(new EmployeeFilterRequest
                {
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    EmployeeStatus = employeeStatus,
                    WorkType = workType,
                    TaskType = taskType,
                    AttendStatus = attendStatus,
                    Month = month,
                    Year = year,
                    FromDate = fromDate,
                    ToDate = toDate
                });

                var total = allData.Count;
                var data = allData.Skip(skip).Take(take).ToList();

                return Json(new
                {
                    success = true,
                    data = data,
                    total = total
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAnalyticsGridData(
            string analysisType, // "worktype" or "tasktype"
            int? employeeId,
            int? month,
            int? year,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                var request = new AttendanceAnalysisRequest
                {
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    FromDate = fromDate,
                    ToDate = toDate
                };

                object data = analysisType?.ToLower() == "tasktype"
                    ? await _elasticSearchService.GetTaskTypeAnalysisAsync(request)
                    : await _elasticSearchService.GetWorkTypeAnalysisAsync(request);

                return Json(new
                {
                    success = true,
                    data = (List<AnalysisBucketResult>)data,
                    total = ((List<AnalysisBucketResult>)data).Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskSummaryGridData(
            int empId,
            string type,
            DateTime date,
            int skip = 0,
            int take = 10)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                var allData = await _elasticSearchService.GetEmployeeTaskSummaryAsync(empId, type, date);
                var total = allData.Count;
                var data = allData.Skip(skip).Take(take).ToList();

                return Json(new
                {
                    success = true,
                    data = data,
                    total = total
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeDropdownList()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                var employees = await _employee.GetAllUsers();
                var dropdownData = employees
                    .Where(e => string.Equals(e.Role, "Employee", StringComparison.OrdinalIgnoreCase))
                    .Select(e => new { id = e.EmployeeId, name = e.Name, email = e.Email })
                    .OrderBy(x => x.name)
                    .ToList();

                return Json(new { success = true, data = dropdownData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReindexAllData()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return Unauthorized(new { success = false, message = "Unauthorized" });

            try
            {
                var allAttendance = await _repo.GetAllAttendance();
                if (allAttendance.Count == 0)
                    return Json(new { success = true, message = "No attendance records to index" });

                int indexedCount = 0;
                int failedCount = 0;

                foreach (var attend in allAttendance)
                {
                    try
                    {
                        var empData = await _employee.GetUserById(attend.EmpId);
                        var result = await _elasticSearchService.IndexAttendanceAsync(
                            attend,
                            empData?.Name,
                            empData?.Email,
                            empData?.Status);
                        if (result) indexedCount++;
                        else failedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error indexing attendance {attend.AttendId}: {ex.Message}");
                        failedCount++;
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Re-indexing complete. Indexed: {indexedCount}, Failed: {failedCount}, Total: {allAttendance.Count}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
