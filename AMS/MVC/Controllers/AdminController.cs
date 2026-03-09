using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;
using Repositories.Models;

namespace MyApp.Namespace
{
    public class AdminController : Controller
    {
        private readonly IAttendenceInterface _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IEmployeeInterface _employee;
        private readonly IDashboardRepository _dashboardRepository;

        public AdminController(IWebHostEnvironment env, IEmployeeInterface employee, IAttendenceInterface repo, IDashboardRepository dashboardRepository)
        {
            _env = env;
            _employee = employee;
            _repo = repo;
            _dashboardRepository = dashboardRepository;
        }
        // GET: AdminController
        public ActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Admin")
            {
                return BadRequest("You Dont Access for this page");
            }
            else{
                var data = _dashboardRepository.GetDashboardData();
                return View(data);
            }
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Admin")
            {
                return BadRequest("You Dont Access for this page");
            }
            else{
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceScheduler(int empId)
        {
            var role = HttpContext.Session.GetString("Role");
            if(role != "Admin")
            {
                return BadRequest("You Dont Access for this page");
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
            if(role != "Admin")
            {
                return BadRequest("You Dont Access for this page");
            }
            else
            {
                System.Console.WriteLine(EmployeeId + "" + Status);
                var result = await _employee.UpdateUserStatus(EmployeeId, Status);
                System.Console.WriteLine(result);

                if (result)
                {
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
            if(role != "Admin")
            {
                return BadRequest("You Dont Access for this page");
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
            if(role != "Admin")
            {
                return BadRequest("You Dont Access for this page");
            }
            else
            {   
                var result = await _dashboardRepository.GetAllUsersForAccess();
                return Ok(new { success = true, data = result });
            }
        }
    }
}
