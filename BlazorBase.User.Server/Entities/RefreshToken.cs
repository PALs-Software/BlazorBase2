using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

[Index(nameof(Token), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(FamilyId))]
public class RefreshToken
{
    public Guid Id { get; set; }

    [Required, MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
}
