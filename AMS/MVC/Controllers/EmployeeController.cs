using Microsoft.AspNetCore.Mvc;

namespace MyApp.Namespace
{
    public class EmployeeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Dashboard()
        {
            return View();
        }

        
    }
}
