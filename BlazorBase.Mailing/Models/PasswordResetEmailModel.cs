namespace BlazorBase.Mailing.Models;

/// <summary>
/// The values of the standard password reset email rendered by <c>PasswordResetEmail</c>. Set
/// <see cref="ResetLink"/> for the link flow or <see cref="ResetCode"/> for the code flow.
/// </summary>
public class PasswordResetEmailModel
{
    /// <summary>The name the recipient is greeted with; a neutral greeting is used when empty.</summary>
    public string? RecipientName { get; set; }

    /// <summary>The link to the client's reset-password page.</summary>
    public string? ResetLink { get; set; }

    /// <summary>The reset code to enter, used when there is no link.</summary>
    public string? ResetCode { get; set; }
}
