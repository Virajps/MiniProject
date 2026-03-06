using System.ComponentModel.DataAnnotations;

namespace Repositories;

public class vm_ChangePassword
{
    [Required]
    public int EmployeeId { get; set; }
    [Required]
    public string? OldPassword { get; set; }
    [Required]
    public string? NewPassword { get; set; }

    [Required]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }
}
