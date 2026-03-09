using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Mvc;
using Repositories;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Models;

namespace MyApp.Namespace
{
    public class EmployeeController : Controller
    {
        private readonly IAttendenceInterface _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IEmployeeInterface _employee;
        // GET: EmployeeController
        public EmployeeController(IWebHostEnvironment env, IEmployeeInterface employee, IAttendenceInterface repo)
        {
            _env = env;
            _employee = employee;
            _repo = repo;
        }

        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Dashboard()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ClockIn(string workType)
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var result = await _repo.ClockIn(empId.Value, workType);

            if (result == 1)
                return Ok(new { success = true, message = "Clock-In successful" });

            if (result == 0)
                return Ok(new { success = false, message = "Already clocked in today" });

            return Ok(new { success = false, message = "Clock-In failed" });
        }

        [HttpPost]
        public async Task<IActionResult> ClockOut(List<string> taskTypes)
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var result = await _repo.ClockOut(empId.Value, taskTypes);

            if (result == 1)
                return Ok(new { success = true, message = "Clock-Out successful" });

            if (result == -2)
                return Ok(new { success = false, message = "Already clocked out today" });

            return Ok(new { success = false, message = "Clock-Out failed" });
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [AcceptVerbs("POST", "PUT")]
        public async Task<IActionResult> ChangePassword([FromForm] vm_ChangePassword changePassword)
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            if (string.IsNullOrWhiteSpace(changePassword.OldPassword) ||
                string.IsNullOrWhiteSpace(changePassword.NewPassword) ||
                string.IsNullOrWhiteSpace(changePassword.ConfirmPassword))
            {
                return BadRequest(new { success = false, message = "All password fields are required." });
            }

            if (!string.Equals(changePassword.NewPassword, changePassword.ConfirmPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { success = false, message = "New password and confirm password do not match." });
            }

            var currentUser = await _employee.GetUserById(empId.Value);
            if (currentUser == null)
            {
                return BadRequest(new { success = false, message = "User not found." });
            }

            if (!string.Equals(currentUser.Password, changePassword.OldPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { success = false, message = "Current password is incorrect." });
            }

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
        [HttpGet]
        public async Task<IActionResult> GetUserById(int EmployeeId)
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
        [HttpDelete]
        public async Task<IActionResult> DeleteUser(int EmployeeId)
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
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
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
        public ActionResult UpdateUser()
        {
            return View();
        }
        [HttpPut]
        public async Task<IActionResult> UpdateUser(int EmployeeId,[FromForm] t_Employee employee)
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
        [HttpPut]
        public async Task<IActionResult> UpdateUserStatus(int EmployeeId, string Status)
        {
            System.Console.WriteLine(EmployeeId + "" + Status);
            var result = await _employee.UpdateUserStatus(EmployeeId, Status);
            System.Console.WriteLine(result);

            if (result != null)
            {
                return Ok(new { sucess = true, message = "Status updated successfull" });
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to update status" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AttendanceChart(string type, DateTime date)
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

            var data = await _repo.GetAttendanceChart(empId.Value, type.ToLowerInvariant(), date);

            if (data != null)
            {
                return Json(data);
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to Load Chart" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> TotalHoursAllYears()
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var attendance = await _repo.GetAttendanceScheduler(empId.Value);
            int totalHours = attendance?.Sum(x => x.WorkingHour) ?? 0;

            return Ok(new
            {
                success = true,
                totalHours = totalHours
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskSummary()
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var data = await _repo.GetEmployeeTaskSummary(empId.Value);
            return Ok(new { success = true, data = data ?? new List<vm_TaskSummary>() });
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
        public async Task<IActionResult> Scheduler()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceScheduler()
        {
            int? empId = HttpContext.Session.GetInt32("EmployeeId");
            if (empId == null || empId <= 0)
            {
                return Unauthorized(new { success = false, message = "Employee session not found." });
            }

            var data = await _repo.GetAttendanceScheduler(empId.Value);

            if (data != null)
            {
                return Json(data);
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to Load History" });
            }
        }
    }
}

