using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Entities;

public class BaseUser : IdentityUser
{
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ThemePreference { get; set; } = "System";

    [MaxLength(10)]
    public string Language { get; set; } = "en";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
