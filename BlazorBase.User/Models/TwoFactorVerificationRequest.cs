using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

public class TwoFactorVerificationRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
}
