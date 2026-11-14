using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

/// <summary>
/// Body of <c>POST change-password</c>: the signed-in account proves it knows the current password and
/// chooses a new one. The password rules of the server's Identity configuration alone decide whether the
/// new password is accepted, so no length is annotated here; the server's reasons come back per field.
/// </summary>
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
