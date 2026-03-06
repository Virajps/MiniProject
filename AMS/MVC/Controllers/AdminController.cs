using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;

namespace MyApp.Namespace
{
    public class AdminController : Controller
    {
        private readonly IAttendenceInterface _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IEmployeeInterface _employee;
        public AdminController(IWebHostEnvironment env, IEmployeeInterface employee, IAttendenceInterface repo)
        {
            _env = env;
            _employee = employee;
            _repo = repo;
        }
        // GET: AdminController
        public ActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAttendanceScheduler(int empId)
        {
            var data = await _repo.GetAttendanceScheduler(empId);

            return Json(data);
        }
    }
}
