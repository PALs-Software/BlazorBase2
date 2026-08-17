namespace BlazorBase.Mailing.Models;

/// <summary>
/// In-memory attachment for an outgoing email.
/// </summary>
public class EmailAttachment(string fileName, byte[] content, string contentType = "application/octet-stream")
{
    public string FileName { get; } = fileName;
    public byte[] Content { get; } = content;
    public string ContentType { get; } = contentType;
}
