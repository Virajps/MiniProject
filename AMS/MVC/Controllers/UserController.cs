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

        private readonly OTPEmailService _otp;
        public UserController(IWebHostEnvironment env, IUserInterface emp, IRedisUserService redis, IRabbitRegistration rabbit, IGmailSmtpSenderInterface email, ElasticSearchService elasticSearch, OTPEmailService otp)
        {
            _empRepo = emp;
            _env = env;
            _redis = redis;
            _rabbit = rabbit;

            _email = email;
            _elasticSearch = elasticSearch;

            _otp = otp;
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
                    await _redis.SetUserAsync(UserData);
                    await _elasticSearch.IndexAttendanceAsync(attendance);
                    HttpContext.Session.SetInt32("EmployeeId", UserData.EmployeeId);
                    HttpContext.Session.SetString("EmployeeName", UserData.Name);
                    HttpContext.Session.SetString("Role", UserData.Role);
                    HttpContext.Session.SetString("ProfileImage", UserData.Image ?? "");
                    if (UserData.Role == "Admin")
                    {
                        return Json(new { success = true, role = UserData.Role });
                    }
                    else
                    {
                        if (UserData.Status == "Active")
                        {
                            return Json(new { success = true, role = UserData.Role });
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
            if (ModelState.IsValid)
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
                if (status == 1)
                {
                    await _redis.SetUserAsync(emp);
                    var cachedUser = await _redis.GetUserAsync(emp.Email ?? string.Empty);
                    using var connection = await _rabbit.GetConnection();

                    await _rabbit.PublishUserRegistrationAsync(connection, emp);
                    await _email.Welcome(toEmail: emp.Email, userName: emp.Name);

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
            return RedirectToAction("Login", "User");
        }
        // Page
        public IActionResult forgetPassword()
        {
            return View();
        }

        // SEND OTP
        [HttpPost]
        public async Task<IActionResult> SendOTP(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Email is required" });
            }

            email = email.Trim();

            var user = await _empRepo.GetUserByEmail(email);
            if (user == null)
            {
                return Json(new { success = false, message = "Email not found" });
            }

            var userName = string.IsNullOrWhiteSpace(user.Name) ? (user.Email ?? email) : user.Name;

            try
            {
                await _otp.SendOTP(email, userName);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendOTP Error: " + ex.Message);
                return Json(new { success = false, message = "Failed to send OTP" });
            }
        }

        // VERIFY OTP
        [HttpPost]
        public async Task<IActionResult> VerifyOTP(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
            {
                return Json(new { success = false, message = "Email and OTP are required" });
            }

            email = email.Trim();
            otp = otp.Trim();

            var result = await _otp.VerifyOTP(email, otp);

            if (result)
                return Json(new { success = true });

            return Json(new { success = false, message = "Invalid or expired OTP" });
        }

        // RESET PASSWORD
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "Email and password are required" });
            }

            email = email.Trim();

            var result = await _otp.ResetPassword(email, password);

            if (result)
                return Json(new { success = true });

            var isVerified = await _redis.IsOtpVerified(email);
            if (!isVerified)
            {
                return Json(new { success = false, message = "OTP not verified or expired" });
            }

            return Json(new { success = false, message = "Password update failed" });
        }
    }
}
