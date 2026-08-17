using BlazorBase.Mailing.Models;
using Xunit;

namespace BlazorBase.Mailing.Test.Models;

/// <summary>Guards the default values of the mailing model types against accidental change.</summary>
public sealed class MailingModelDefaultsTests
{
    [Fact]
    public void EmailAttachment_DefaultsContentType_ToOctetStream()
    {
        var attachment = new EmailAttachment("file.bin", [1, 2, 3]);

        Assert.Equal("file.bin", attachment.FileName);
        Assert.Equal([1, 2, 3], attachment.Content);
        Assert.Equal("application/octet-stream", attachment.ContentType);
    }

    [Fact]
    public void SmtpSettings_HaveSecureDefaults()
    {
        var settings = new SmtpSettings();

        Assert.Equal(587, settings.Port);
        Assert.True(settings.UseStartTls);
        Assert.False(settings.AllowInvalidServerCertificate);
    }

    [Fact]
    public void EmailMessage_InitializesCollections_AndEmptySubject()
    {
        var message = new EmailMessage();

        Assert.Empty(message.To);
        Assert.Empty(message.Cc);
        Assert.Empty(message.Bcc);
        Assert.Empty(message.Attachments);
        Assert.Equal(string.Empty, message.Subject);
        Assert.Null(message.PlainTextBody);
    }
}
