using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
