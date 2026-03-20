using System.Runtime.Intrinsics.Arm;
using Microsoft.AspNetCore.Mvc;
using Repositories;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Services;

namespace MyApp.Namespace
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class UserController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IUserInterface _empRepo;
        private readonly IRedisUserService _redis;
        private readonly IRabbitRegistration _rabbit;
        private readonly ElasticSearchService _elasticSearch; 
        private readonly IGmailSmtpSenderInterface _email;
        public UserController(IWebHostEnvironment env, IUserInterface emp ,IRedisUserService redis,IRabbitRegistration rabbit, IGmailSmtpSenderInterface email, ElasticSearchService elasticSearch)
        {
            _empRepo = emp;
            _env = env;
            _redis=redis;
            _rabbit=rabbit;

            _email = email;
            _elasticSearch = elasticSearch;
        }

        // GET: UserController
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Login()
        {
            return View();
        }

        public ActionResult Register()
        {
            return View();
        }

        public ActionResult Unauthorized()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(vm_login login)
        {
            t_Employee UserData = await _empRepo.LoginUser(login);
            t_Attendance attendance = new t_Attendance();
            if (ModelState.IsValid)
            {
                if (UserData.EmployeeId != 0)
                {
                    await _elasticSearch.IndexAttendanceAsync(attendance);
                    HttpContext.Session.SetInt32("EmployeeId", UserData.EmployeeId);
                    HttpContext.Session.SetString("EmployeeName", UserData.Name);
                    HttpContext.Session.SetString("Role", UserData.Role);
                    HttpContext.Session.SetString("ProfileImage", UserData.Image ?? "");
                    if(UserData.Role == "Admin")
                    {
                        return Json(new {success=true,role=UserData.Role});
                    }
                    else
                    {
                        if(UserData.Status == "Active")
                        {
                            return Json(new {success=true,role=UserData.Role});
                        }
                        else
                        {
                            return Json(new { success = false, message = "Employee is Inactive" });
                        }
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Invalid Employee And Password" });
                }
            }
            else
            {
               var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        k => k.Key,
                        v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return Json(new
                {
                    success = false,
                    errors = errors
                }); 
            }
            
        }

        [HttpPost]
        public async Task<IActionResult> Register(t_Employee emp)
        {
            if(ModelState.IsValid)
        {
            if (emp.ImageFile != null && emp.ImageFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "profile_images");

                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(emp.ImageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await emp.ImageFile.CopyToAsync(stream);
                }

                emp.Image = fileName;
            }
            Console.WriteLine("user.c_fname: " + emp.Name);

            var status = await _empRepo.RegisterUser(emp);
            if(status == 1)
            {
                await _redis.SetUserAsync(emp);
                var cachedUser=await _redis.GetUserAsync(emp.Email??string.Empty);
            using var connection = await _rabbit.GetConnection();

            await _rabbit.PublishUserRegistrationAsync(connection, emp);
                await _email.Welcome(toEmail: emp.Email, userName: emp.Name);
                Console.WriteLine("Registration successful for: " + emp.Email);

                return Json(new { success = true, message = "Registration Successful" });
            }
            else if (status == 0)
            {
                return Json(new { success = false, message = "Already Registered" });
            }
            else
            {
               return Json(new { success = false, message = "error " }); 
            }
            }
            else
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        k => k.Key,
                        v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return Json(new
                {
                    success = false,
                    errors = errors
                });
            }
        }
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login","User");
        }
    }
}
