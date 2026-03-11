using System.ComponentModel.DataAnnotations;

namespace Repositories;

public class vm_ChangePassword
{
    [Required]
    public int EmployeeId { get; set; }
    [Required]
    public string? OldPassword { get; set; }
    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$",
        ErrorMessage = "Password must contain at least 8 characters, including uppercase, lowercase, number, and special character.")]
    public string? NewPassword { get; set; }

    [Required]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }
}
