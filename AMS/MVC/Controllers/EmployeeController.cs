using System.Threading.Tasks.Dataflow;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Repositories;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Services;
using Microsoft.Extensions.DependencyInjection;


namespace MyApp.Namespace
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class EmployeeController : Controller
    {
        private static readonly Regex StrongPasswordRegex = new(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
            RegexOptions.Compiled);

        private readonly IAttendenceInterface _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IEmployeeInterface _employee;
        private readonly ElasticSearchService _elasticSearchService;
        // GET: EmployeeController
        public EmployeeController(
            IWebHostEnvironment env,
            IEmployeeInterface employee,
            IAttendenceInterface repo,
            ElasticSearchService elasticSearchService)
        {
            _env = env;
            _employee = employee;
            _repo = repo;
            _elasticSearchService = elasticSearchService;
        }

        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Dashboard()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else{
                return View();
            }
        }
        [HttpPost]
        public async Task<IActionResult> ClockIn(string workType)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                int? empId = HttpContext.Session.GetInt32("EmployeeId");
                if (empId == null || empId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Employee session not found." });
                }

                var result = await _repo.ClockIn(empId.Value, workType);

                if (result == 1)
                {
                    var attendanceCacheService = HttpContext.RequestServices.GetRequiredService<IAttedanceCacheService>();
                    var employeeName = HttpContext.Session.GetString("EmployeeName");
                    await attendanceCacheService.SetEmployeeNameAsync(empId.Value, employeeName ?? string.Empty);
                    var cachedClockIn = await attendanceCacheService.GetClockInAsync(empId.Value);
                    if (cachedClockIn != null)
                    {
                        await attendanceCacheService.SetClockInAsync(
                            empId.Value,
                            employeeName ?? string.Empty,
                            cachedClockIn.ClockInTime,
                            cachedClockIn.WorkType,
                            cachedClockIn.Status);
                    }
                    var rabbitService = HttpContext.RequestServices.GetRequiredService<IRabbitRegistration>();
                    await using var rabbitConnection = await rabbitService.GetConnection();
                    await rabbitService.PublishAttendanceEventAsync(rabbitConnection, empId.Value, "ClockIn", new
                    {
                        WorkType = workType,
                        ClockInTime = DateTime.UtcNow
                    });
                    return Ok(new { success = true, message = "Clock-In successful" });
                }

                if (result == 0)
                    return Ok(new { success = false, message = "Already clocked in today" });

                return Ok(new { success = false, message = "Clock-In failed" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClockOut(List<string> taskTypes)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else{
                int? empId = HttpContext.Session.GetInt32("EmployeeId");
                if (empId == null || empId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Employee session not found." });
                }

                var result = await _repo.ClockOut(empId.Value, taskTypes);

                if (result == 1)
                {
                    var attendanceCacheService = HttpContext.RequestServices.GetRequiredService<IAttedanceCacheService>();
                    var employeeName = HttpContext.Session.GetString("EmployeeName");
                    await attendanceCacheService.SetEmployeeNameAsync(empId.Value, employeeName ?? string.Empty);
                    var rabbitService = HttpContext.RequestServices.GetRequiredService<IRabbitRegistration>();
                    await using var rabbitConnection = await rabbitService.GetConnection();
                    await rabbitService.PublishAttendanceEventAsync(rabbitConnection, empId.Value, "ClockOut", new
                    {
                        TaskTypes = taskTypes,
                        ClockOutTime = DateTime.UtcNow
                    });
                    return Ok(new { success = true, message = "Clock-Out successful" });
                }

                if (result == -2)
                    return Ok(new { success = false, message = "Already clocked out today" });

                return Ok(new { success = false, message = "Clock-Out failed" });
            }
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                return View();
            }
        }

        [AcceptVerbs("POST", "PUT")]
        public async Task<IActionResult> ChangePassword([FromForm] vm_ChangePassword? changePassword)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                int? empId = HttpContext.Session.GetInt32("EmployeeId");
                if (empId == null || empId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Employee session not found." });
                }

                if (changePassword == null)
                {
                    return BadRequest(new { success = false, message = "Invalid request payload." });
                }

                var oldPassword = changePassword.OldPassword?.Trim();
                var newPassword = changePassword.NewPassword?.Trim();
                var confirmPassword = changePassword.ConfirmPassword?.Trim();

                if (string.IsNullOrWhiteSpace(oldPassword) ||
                    string.IsNullOrWhiteSpace(newPassword) ||
                    string.IsNullOrWhiteSpace(confirmPassword))
                {
                    return BadRequest(new { success = false, message = "All password fields are required." });
                }

                if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
                {
                    return BadRequest(new { success = false, message = "New password and confirm password do not match." });
                }

                if (!StrongPasswordRegex.IsMatch(newPassword))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Password must be minimum 8 characters and include 1 uppercase, 1 lowercase, 1 digit, and 1 special character."
                    });
                }

                var currentUser = await _employee.GetUserById(empId.Value);
                if (currentUser == null)
                {
                    return BadRequest(new { success = false, message = "User not found." });
                }

                if (!string.Equals(currentUser.Password, oldPassword, StringComparison.Ordinal))
                {
                    return BadRequest(new { success = false, message = "Current password is incorrect." });
                }

                changePassword.OldPassword = oldPassword;
                changePassword.NewPassword = newPassword;
                changePassword.ConfirmPassword = confirmPassword;
                changePassword.EmployeeId = empId.Value;

                var result = await _employee.ChangePassword(changePassword);
                if (result == 0)
                {
                    return Ok(new { success = false, message = "Failed to update password." });
                }
                else if (result > 0)
                {
                    return Ok(new { success = true, message = "Password updated successfully." });
                }
                else
                {
                    return Ok(new { success = false, message = "Failed to change password." });
                }
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetUserById(int EmployeeId)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {

                var result = await _employee.GetUserById(EmployeeId);
                if (result != null)
                {
                    System.Console.WriteLine("User data fetched" + EmployeeId);
                    return Ok(new { success = true, data = result });
                }
                else if (result == null)
                {
                    return Ok(new { success = false, message = "Data not fetched" });
                }
                else
                {
                    return Ok(new { success = false, message = "Internal Server Error while GetUserById" });
                }
            }
        }
        [HttpDelete]
        public async Task<IActionResult> DeleteUser(int EmployeeId)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                var result = await _employee.DeleteUser(EmployeeId);
                if (result == 1)
                {
                    return Ok(new { success = true, message = "Employee Deleted Successfull!" });
                }
                else if (result == 0)
                {
                    return Ok(new { success = true, message = "Internal Server Error while deleting employee" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Error while deleting employee" });
                }
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                try
                {
                    var result = await _employee.GetAllUsers();
                    return Ok(new { success = true, data = result });
                }
                catch (Exception ex)
                {
                    return Ok(new { success = false, message = "Internal Server Error while GetAllUsers" + ex.Message });
                }
            }
        }
        public ActionResult UpdateUser()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else{
                return View();
            }
        }
        [HttpPut]
        public async Task<IActionResult> UpdateUser(int EmployeeId,[FromForm] t_Employee employee)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                if (employee.ImageFile != null && employee.ImageFile.Length > 0)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "profile_images");

                    if (!Directory.Exists(uploads))
                        Directory.CreateDirectory(uploads);

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(employee.ImageFile.FileName);
                    var filePath = Path.Combine(uploads, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await employee.ImageFile.CopyToAsync(stream);
                    }

                    employee.Image = fileName;
                }
                var result = await _employee.UpdateUser(EmployeeId, employee);

                if (result == true)
                {
                    return Ok(new { success = true, message = "Employee Updated Successfull!" });
                }
                else
                {
                    return Ok(new { success = false, message = "Internal server error while updating" });
                }
            }
        }


        [HttpGet]
        public async Task<IActionResult> AttendanceChart(string type, DateTime date)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                int? empId = HttpContext.Session.GetInt32("EmployeeId");
                if (empId == null || empId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Employee session not found." });
                }

                if (string.IsNullOrWhiteSpace(type))
                {
                    type = "week";
                }

                if (date == default)
                {
                    date = DateTime.Today;
                }

                // var data = await _repo.GetAttendanceChart(empId.Value, type.ToLowerInvariant(), date);
                var data = await _elasticSearchService.GetAttendanceChartAsync(empId.Value, type.ToLowerInvariant(), date);

                if (data != null)
                {
                    return Json(data);
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to Load Chart" });
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> TotalHoursAllYears()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                int? empId = HttpContext.Session.GetInt32("EmployeeId");
                if (empId == null || empId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Employee session not found." });
                }

                // Return all-time total hours till now.
                // var attendance = await _repo.GetAttendanceScheduler1(empId.Value);
                // int totalHours = attendance?.Sum(x => x.WorkingHour) ?? 0;
                int totalHours = await _elasticSearchService.GetTotalWorkingHoursAsync(empId.Value);

                return Ok(new
                {
                    success = true,
                    totalHours = totalHours
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskSummary(string type = "week", DateTime? date = null)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Employee")
            {
                return RedirectToAction("Unauthorized","User");
            }
            else
            {
                int? empId = HttpContext.Session.GetInt32("EmployeeId");
                if (empId == null || empId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Employee session not found." });
                }

                var effectiveType = string.IsNullOrWhiteSpace(type) ? "week" : type.ToLowerInvariant();
                var effectiveDate = date ?? DateTime.Today;

                // var data = await _repo.GetEmployeeTaskSummary(empId.Value, effectiveType, effectiveDate);
                var data = await _elasticSearchService.GetEmployeeTaskSummaryAsync(empId.Value, effectiveType, effectiveDate);
                return Ok(new { success = true, data = data ?? new List<vm_TaskSummary>() });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceSummary()
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var data = await _repo.GetEmployeeAttendanceSummary(empId.Value);
            return Ok(new { success = true, data = data ?? new vm_AttendenceSummary() });
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceScheduler()
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var data = await _repo.GetAttendanceScheduler1(empId.Value);

            if (data != null)
            {
                return Json(data);
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to Load History" });
            }
        }

        //------------- Kendo Grid Methods - ElasticSearch Data Binding ---------//

        [HttpGet]
        public async Task<IActionResult> GetAttendanceChartGridData(string type, DateTime date)
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
                return Unauthorized(new { success = false, message = "Employee session not found." });

            try
            {
                if (string.IsNullOrWhiteSpace(type))
                    type = "week";

                if (date == default)
                    date = DateTime.Today;

                var chartData = await _elasticSearchService.GetAttendanceChartAsync(empId.Value, type, date);

                return Json(new
                {
                    success = true,
                    data = chartData?.ChartData ?? new List<vm_AttendanceChart>(),
                    totalHours = chartData?.TotalHours ?? 0,
                    lateInCount = chartData?.LateInCount ?? 0,
                    earlyOutCount = chartData?.EarlyOutCount ?? 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskSummaryGridData(string type, DateTime date, int skip = 0, int take = 10)
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
                return Unauthorized(new { success = false, message = "Employee session not found." });

            try
            {
                if (string.IsNullOrWhiteSpace(type))
                    type = "month";

                if (date == default)
                    date = DateTime.Today;

                var allData = await _elasticSearchService.GetEmployeeTaskSummaryAsync(empId.Value, type, date);
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
        public async Task<IActionResult> GetTotalWorkingHoursGridData()
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
                return Unauthorized(new { success = false, message = "Employee session not found." });

            try
            {
                var totalHours = await _elasticSearchService.GetTotalWorkingHoursAsync(empId.Value);

                return Json(new
                {
                    success = true,
                    totalWorkingHours = totalHours
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
