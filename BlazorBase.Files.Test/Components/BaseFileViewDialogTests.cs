using BlazorBase.Components.Test.Infrastructure;
using BlazorBase.Files.Components.BaseFileModal;
using BlazorBase.Files.Test.Infrastructure;
using Bunit;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Files.Test.Components;

public class BaseFileViewDialogTests : FilesBunitTestContextBase
{
    private const string DownloadUrl = "/api/files/00000000-0000-0000-0000-000000000001?token=t";

    [Fact]
    public void ImageFile_ShowsTheImageFromTheDownloadUrl()
    {
        var harness = DialogHarness<BaseFileViewDialog>.Open(this, "View file", ContentFor(Image()));

        var image = harness.Content.Find("img.modal-image");
        Assert.Equal(DownloadUrl, image.GetAttribute("src"));
        Assert.Equal("Image", image.GetAttribute("alt"));
        Assert.Empty(harness.Content.FindAll(".modal-file-info"));
    }

    [Fact]
    public void NonImageFile_ShowsTheFileNameInsteadOfAnImage()
    {
        var harness = DialogHarness<BaseFileViewDialog>.Open(this, "View file", ContentFor(Document()));

        Assert.Empty(harness.Content.FindAll("img"));
        Assert.Contains("report.pdf", harness.Content.Find(".modal-file-info").TextContent);
    }

    [Fact]
    public void TheDownloadButton_LinksToTheDownloadUrl()
    {
        var harness = DialogHarness<BaseFileViewDialog>.Open(this, "View file", ContentFor(Image()));

        var download = harness.Content.FindComponent<FluentAnchorButton>().Instance;

        Assert.Equal(DownloadUrl, download.Href);
        Assert.True(download.AdditionalAttributes!.ContainsKey("download"));
    }

    [Fact]
    public async Task ClickingClose_ClosesTheDialogAsCancelled()
    {
        var harness = DialogHarness<BaseFileViewDialog>.Open(this, "View file", ContentFor(Image()));

        harness.Content.FindComponents<FluentButton>().Single(button => button.Caption() == "Close").Click();

        Assert.True((await harness.Result).Cancelled);
    }

    private static FileViewDialogContent ContentFor(Models.FileUploadResult file) => new()
    {
        File = file,
        DownloadUrl = DownloadUrl,
        ImageAltText = "Image",
        DownloadText = "Download",
        CloseText = "Close",
    };
}
