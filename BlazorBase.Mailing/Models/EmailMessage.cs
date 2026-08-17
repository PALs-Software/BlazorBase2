namespace BlazorBase.Mailing.Models;

/// <summary>
/// Transport-agnostic representation of an outgoing email.
/// </summary>
public class EmailMessage
{
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public List<EmailAttachment> Attachments { get; set; } = [];
}
