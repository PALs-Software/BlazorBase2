namespace BlazorBase.Mailing.Models;

/// <summary>
/// The values of the standard "your password was changed" notification rendered by
/// <c>PasswordChangedEmail</c>. The email carries no link and no token: it only tells the owner of the
/// account that the password changed and what to do when that was not them.
/// </summary>
public class PasswordChangedEmailModel
{
    /// <summary>The name the recipient is greeted with; a neutral greeting is used when empty.</summary>
    public string? RecipientName { get; set; }
}
