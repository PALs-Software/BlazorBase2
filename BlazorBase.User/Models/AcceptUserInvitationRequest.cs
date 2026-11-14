using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

/// <summary>The body of <c>POST api/auth/invitations/accept</c>.</summary>
public class AcceptUserInvitationRequest
{
    [Required]
    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
