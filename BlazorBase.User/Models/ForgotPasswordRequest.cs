using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
