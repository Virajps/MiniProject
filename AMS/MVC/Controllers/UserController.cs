using Microsoft.AspNetCore.Mvc;
using Repositories;
using Repositories.Interfaces;
using Repositories.Models;

namespace MyApp.Namespace
{
    public class UserController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IUserInterface _empRepo;

        public UserController(IWebHostEnvironment env, IUserInterface emp)
        {
            _empRepo = emp;
            // myconfig = confi;
            _env = env;
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

        [HttpPost]
        public async Task<IActionResult> Login(vm_login login)
        {
            t_Employee UserData = await _empRepo.LoginUser(login);
            if (ModelState.IsValid)
            {
                if (UserData.c_UserId != 0)
                {
                    HttpContext.Session.SetInt32("EmployeeId", UserData.EmployeeId);
                    HttpContext.Session.SetString("EmployeeName", UserData.EmployeeName);

                    // return RedirectToAction("Index", "Contact");
                    // return RedirectToAction("Index","ContactSingle");
                }
                else
                {
                    ViewData["message"] = "Invali Employee And Password";
                }
            }
            return View(login);
        }

        [HttpPost]
        public async Task<IActionResult> Register(t_User emp)
        {
            if(ModelState.IsValid)
        {
            if (emp.ProfilePicture != null && emp.ProfilePicture.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "profile_images");

                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(emp.ProfilePicture.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await emp.ProfilePicture.CopyToAsync(stream);
                }

                emp.c_Image = fileName;
            }
            Console.WriteLine("user.c_fname: " + emp.c_UserName);
            var status = await _userRepo.Register(emp);
            if(status == 1)
            {
                ViewData["message"] = "User Registred";
                return RedirectToAction("Login");
            }
            else if (status == 0)
            {
                ViewData["message"] = "User Already Registred";
            }
            else
            {
                ViewData["message"] = "There was some error while Registration"; 
            }
        }
        return View();
        }
    }
}
