using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Controllers;
using BlazorBase.Files.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Files.Server.Test.Controllers;

/// <summary>
/// Thin concrete controller deriving from the abstract <see cref="BaseFileControllerBase"/>,
/// mirroring how a host application discovers the base controller via ASP.NET Core controller
/// scanning. Exists only so tests can instantiate and exercise the base controller's behavior.
/// </summary>
public sealed class TestFileController(
    IFileStorage fileStorage,
    IImageService imageService,
    IFileAccessAuthorizer fileAccessAuthorizer,
    IFileAccessTokenService fileAccessTokenService,
    IBlazorBaseFileOptions options,
    DbContext dbContext) : BaseFileControllerBase(fileStorage, imageService, fileAccessAuthorizer, fileAccessTokenService, options, dbContext);
