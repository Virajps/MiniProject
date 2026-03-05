using System.ComponentModel.DataAnnotations;
namespace Repositories;

public class vm_login
{
    [Required(ErrorMessage = "Email is required")]
    public string? UserEmail { get; set; }
    

    [Required(ErrorMessage = "Password is required")]
    public string? UserPassword { get; set; }

    public string? UserRole { get; set; }
}
