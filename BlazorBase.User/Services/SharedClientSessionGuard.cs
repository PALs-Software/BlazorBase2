using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Services;

/// <summary>
/// Refuses the process-wide client session in a process that hosts an ASP.NET Core server.
/// </summary>
/// <remarks>
/// The shared <see cref="CsrfTokenProvider"/> and <see cref="AuthenticationSessionState"/> are correct
/// where one process serves one user (WebAssembly, MAUI) and a cross-user session leak where one process
/// serves every user. A process counts as a server when its container has
/// <c>Microsoft.AspNetCore.Hosting.Server.IServer</c> registered. The interface is looked up by name
/// because this library has no ASP.NET Core hosting reference and has to stay loadable in the browser;
/// registration is checked without instantiating the server.
/// </remarks>
internal static class SharedClientSessionGuard
{
    private const string ServerTypeName = "Microsoft.AspNetCore.Hosting.Server.IServer, Microsoft.AspNetCore.Hosting.Server.Abstractions";

    private const string RefusalMessage =
        "AddBlazorBaseUserSharedClientSession() is registered in a process that hosts an ASP.NET Core server. " +
        "It is called by AddBlazorBaseUserWasm, AddBlazorBaseUserWasmCookieAuth and AddBlazorBaseUserMaui and makes " +
        "the antiforgery token and the authentication state one instance for the whole process. In a server one " +
        "process serves every user, so all users would share one session. Register these only in the WebAssembly " +
        "or MAUI client. A server-rendered host calls AddBlazorBaseUserClient() alone, which keeps the state " +
        "scoped, and registers a scoped ITokenStorage of its own.";

    /// <summary>
    /// Throws when the process behind <paramref name="serviceProvider"/> hosts an ASP.NET Core server.
    /// </summary>
    /// <param name="serviceProvider">The container that is about to create the shared session state.</param>
    /// <exception cref="InvalidOperationException">The container has an ASP.NET Core server registered.</exception>
    public static void ThrowIfServerProcess(IServiceProvider serviceProvider)
    {
        if (OperatingSystem.IsBrowser())
            return;

        var serverType = Type.GetType(ServerTypeName, throwOnError: false);

        if (serverType is null)
            return;

        if (!IsRegistered(serviceProvider, serverType))
            return;

        throw new InvalidOperationException(RefusalMessage);
    }

    private static bool IsRegistered(IServiceProvider serviceProvider, Type serviceType)
    {
        var serviceProviderIsService = serviceProvider.GetService<IServiceProviderIsService>();

        if (serviceProviderIsService is not null)
            return serviceProviderIsService.IsService(serviceType);

        return serviceProvider.GetService(serviceType) is not null;
    }
}
