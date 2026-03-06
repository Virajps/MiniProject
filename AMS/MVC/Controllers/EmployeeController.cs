using Microsoft.AspNetCore.Mvc;
using Repositories;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Models;

namespace MyApp.Namespace
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeInterface _employee;
        // GET: EmployeeController
        public EmployeeController(IEmployeeInterface employee)
        {
            _employee = employee;
        }
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Dashboard()
        {
            return View();
        }
        [HttpPut]
        public async Task<IActionResult> ChangePassword(vm_ChangePassword changePassword)
        {
            
            var result = await _employee.ChangePassword(changePassword);
            if(result == 0)
            {
                return Ok(new{success=false, message="Internal Server Error"});
            }
            else if(result > 0){
                return Ok(new{success=true, message="Passsword Updated Successfull!"});
            }
            else
            {
                return Ok(new{success=false, message="failed to change password"});
            }
        }
        
    }
}
