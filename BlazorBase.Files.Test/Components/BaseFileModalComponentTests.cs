using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Test.Infrastructure;
using BlazorBase.Files.Components.BaseFileModal;
using BlazorBase.Files.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.Files.Test.Components;

public class BaseFileModalComponentTests : FilesBunitTestContextBase
{
    private readonly RecordingDialogService DialogService;

    public BaseFileModalComponentTests()
    {
        var provider = new ServiceCollection().AddSingleton(JSInterop.JSRuntime).BuildServiceProvider();
        DialogService = new RecordingDialogService(provider);
        Services.AddSingleton<IDialogService>(DialogService);
    }

    [Fact]
    public async Task OpenAsync_FetchesTheAccessToken_AndOpensTheViewDialogWithATokenBearingUrl()
    {
        var file = Image();
        var cut = Render<BaseFileModalComponent>();

        await cut.InvokeAsync(() => cut.Instance.OpenAsync(file));

        await UploadClient.Received(1).GetAccessTokenAsync(file.Id, Arg.Any<CancellationToken>());
        var (component, options) = Assert.Single(DialogService.Shown);
        Assert.Equal(typeof(BaseFileViewDialog), component);
        var content = Assert.IsType<FileViewDialogContent>(options.Parameters["Content"]);
        Assert.Equal(file, content.File);
        Assert.Contains($"/api/files/{file.Id}", content.DownloadUrl);
        Assert.Contains($"token={AccessToken}", content.DownloadUrl);
    }

    [Fact]
    public async Task OpenAsync_OpensTheDialogWithTheSharedBaseDialogShape()
    {
        using var english = new EnglishCulture();
        var cut = Render<BaseFileModalComponent>();

        await cut.InvokeAsync(() => cut.Instance.OpenAsync(Document()));

        var options = Assert.Single(DialogService.Shown).Options;
        Assert.Equal("View file", options.Header.Title);
        Assert.Contains(BaseDialogOptions.DialogClass, options.Class);
        Assert.True(options.Header.CloseAction.Visible);
    }

    [Fact]
    public void TheComponent_RendersNoMarkup()
    {
        var cut = Render<BaseFileModalComponent>();

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public async Task CloseAsync_WithoutAnOpenDialog_DoesNothing()
    {
        var cut = Render<BaseFileModalComponent>();

        await cut.InvokeAsync(() => cut.Instance.CloseAsync());

        Assert.Empty(DialogService.Shown);
    }
}
