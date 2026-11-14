namespace BlazorBase.Mailing.Models;

/// <summary>The values of the standard email confirmation email rendered by <c>EmailConfirmationEmail</c>.</summary>
public class EmailConfirmationEmailModel
{
    /// <summary>The name the recipient is greeted with; a neutral greeting is used when empty.</summary>
    public string? RecipientName { get; set; }

    /// <summary>The link to the client's confirm-email page.</summary>
    public string ConfirmationLink { get; set; } = string.Empty;
}
