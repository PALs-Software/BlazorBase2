using BlazorBase.CRUD.Extensions;
using BlazorBase.Files.Components.BaseFileCell;
using BlazorBase.Files.Components.BaseFileGalleryInput;
using BlazorBase.Files.Components.BaseFilePhotoInput;
using BlazorBase.Files.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Files.Services;

public static class BlazorBaseFilesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the BlazorBase.Files client module: options, the HTTP upload client, and all
    /// custom card inputs/displays with the CRUD card seam.
    /// </summary>
    /// <remarks>
    /// The host must register a named or typed <see cref="System.Net.Http.HttpClient"/> for
    /// <see cref="HttpFileUploadClient"/> using
    /// <c>AddHttpClient&lt;IFileUploadClient, HttpFileUploadClient&gt;(…)</c> with the correct
    /// base address and authentication handler (<c>AuthTokenHandler</c>). This method registers
    /// everything else so that a <c>BaseCard</c> rendering a <see cref="Guid"/> property marked
    /// with a file-input attribute automatically renders the FluentUI upload input.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to override default <see cref="BlazorBaseFileOptions"/>.</param>
    public static IServiceCollection AddBlazorBaseFiles(
        this IServiceCollection services,
        Action<IBlazorBaseFileOptions>? configure = null)
    {
        var options = new BlazorBaseFileOptions();
        configure?.Invoke(options);

        services.AddSingleton<IBlazorBaseFileOptions>(options);

        services.AddBlazorBaseCustomInput<BaseFilePhotoInputComponent>();
        services.AddBlazorBaseCustomInput<BaseFileGalleryInputComponent>();
        services.AddBlazorBaseCustomDisplay<BaseFileCellComponent>();

        return services;
    }
}
