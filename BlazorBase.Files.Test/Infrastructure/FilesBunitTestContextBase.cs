using BlazorBase.Components.Services;
using BlazorBase.Files.Models;
using BlazorBase.Files.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BlazorBase.Files.Test.Infrastructure;

/// <summary>
/// Base bUnit context for the file components: the Fluent UI services the way an application registers them, the
/// default file options and an upload client that hands out the access token <see cref="AccessToken"/>.
/// </summary>
public abstract class FilesBunitTestContextBase : BunitContext
{
    protected const string AccessToken = "signed-token";

    protected FilesBunitTestContextBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazorBaseFluentUI();
        Services.AddLocalization();
        Services.AddSingleton<IBlazorBaseFileOptions>(new BlazorBaseFileOptions { ControllerRoute = "api/files" });

        UploadClient = Substitute.For<IFileUploadClient>();
        UploadClient.GetAccessTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(AccessToken));
        Services.AddSingleton(UploadClient);
    }

    protected IFileUploadClient UploadClient { get; }

    protected static FileUploadResult Image(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "photo", ".png", "image/png", 1024, "abc123");

    protected static FileUploadResult Document(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "report", ".pdf", "application/pdf", 2048, "def456");
}
