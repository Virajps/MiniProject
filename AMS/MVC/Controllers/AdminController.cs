using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;

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
            var data = _dashboardRepository.GetDashboardData();
            return View(data);
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
