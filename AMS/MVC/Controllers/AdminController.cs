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
            IRabbitRegistration rabbitRegistration)
        {
            _env = env;
            _employee = employee;
            _repo = repo;
            _dashboardRepository = dashboardRepository;
            _email = email;
            _elasticSearchService = elasticSearchService;
            _rabbitRegistration=rabbitRegistration;
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
                // var data = await _dashboardRepository.GetEmployeeProgress(empId, month, year);
                var data = await _elasticSearchService.GetMonthlyReportAsync(empId, month, year);
                return Json(data);
            }
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
    }
}
    




    
