using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

public class ResendConfirmationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
