using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// A host environment whose name the test chooses, so the Development-only paths can be
/// exercised from both sides of that gate.
/// </summary>
public class StubHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "BlazorBase.User.Server.Test";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
