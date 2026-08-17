using System.Security.Claims;
using System.Security.Cryptography;
using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using BlazorBase.Files.Server.Test.Infrastructure;
using BlazorBase.Files.Server.Test.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Files.Server.Test.Controllers;

public sealed class BaseFileControllerBaseTests : IDisposable
{
    private readonly SqliteConnection Connection;
    private readonly TestFilesDbContext DbContext;
    private readonly OwnerScopedFileAccessAuthorizer Authorizer;
    private readonly HmacFileAccessTokenService TokenService;

    public BaseFileControllerBaseTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var dbContextOptions = new DbContextOptionsBuilder<TestFilesDbContext>()
            .UseSqlite(Connection)
            .Options;

        DbContext = new TestFilesDbContext(dbContextOptions);
        DbContext.Database.EnsureCreated();

        Authorizer = new OwnerScopedFileAccessAuthorizer(DbContext);
        TokenService = new HmacFileAccessTokenService(Options.Create(new FileAccessTokenOptions
        {
            SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }));
    }

    private static ClaimsPrincipal PrincipalFor(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    private static IFormFile CreateFormFile(byte[] content, string fileName, string contentType) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };

    private TestFileController CreateController(IBlazorBaseFileOptions options, StubFileStorage fileStorage, ClaimsPrincipal principal) =>
        new(fileStorage, new ImageSharpImageService(options), Authorizer, TokenService, options, DbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    private async Task<Guid> SeedFileOwnedByAsync(string ownerScopeKey)
    {
        var fileId = Guid.NewGuid();
        DbContext.Files.Add(new BaseFile
        {
            Id = fileId,
            FileName = "owned-file",
            Extension = ".txt",
            ContentType = "text/plain",
            OwnerScopeKey = ownerScopeKey,
        });
        await DbContext.SaveChangesAsync();

        return fileId;
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_AndPersistsNoRow_WhenOwnerScopeKeyExceedsMaxLength()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = false };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        var file = CreateFormFile([1, 2, 3], "notes.txt", "text/plain");
        var overlongOwnerScopeKey = new string('a', 201);

        var result = await controller.Upload(file, overlongOwnerScopeKey, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequestResult.Value);
        Assert.Contains("ownerScopeKey exceeds the maximum length", message);
        Assert.False(fileStorage.CommitAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_AndPersistsNoRow_WhenExtensionExceedsMaxLength()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = false };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        var overlongExtensionFileName = "file." + new string('a', 25);
        var file = CreateFormFile([1, 2, 3], overlongExtensionFileName, "text/plain");

        var result = await controller.Upload(file, null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequestResult.Value);
        Assert.Contains("extension exceeds the maximum length", message);
        Assert.False(fileStorage.CommitAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_AndPersistsNoRow_WhenFileNameExceedsMaxLength()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = false };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        var overlongFileName = new string('a', 261) + ".txt";
        var file = CreateFormFile([1, 2, 3], overlongFileName, "text/plain");

        var result = await controller.Upload(file, null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequestResult.Value);
        Assert.Contains("File name exceeds the maximum length", message);
        Assert.False(fileStorage.CommitAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_AndPersistsNoRow_WhenContentTypeExceedsMaxLength()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = false, AllowedContentTypes = [] };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        var overlongContentType = "text/" + new string('a', 150);
        var file = CreateFormFile([1, 2, 3], "notes.txt", overlongContentType);

        var result = await controller.Upload(file, null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequestResult.Value);
        Assert.Contains("Content type exceeds the maximum length", message);
        Assert.False(fileStorage.CommitAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenDeclaredImageContentIsNotDecodable()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = true, ValidateContentTypeSignature = false };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        byte[] notAnImageBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];
        var file = CreateFormFile(notAnImageBytes, "photo.png", "image/png");

        var result = await controller.Upload(file, null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequestResult.Value);
        Assert.Contains("could not be processed as a valid image", message);
        Assert.False(fileStorage.CommitAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenContentBytesDoNotMatchDeclaredContentType()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = false, ValidateContentTypeSignature = true };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        byte[] jpegHeaderBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
        var file = CreateFormFile(jpegHeaderBytes, "photo.png", "image/png");

        var result = await controller.Upload(file, null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = Assert.IsType<string>(badRequestResult.Value);
        Assert.Contains("does not match the declared content type", message);
        Assert.False(fileStorage.CommitAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    [Fact]
    public async Task Upload_AllowsContentTypeMismatch_WhenSignatureValidationDisabled()
    {
        var options = new BlazorBaseFileOptions { UseImageThumbnails = false, ValidateContentTypeSignature = false };
        var fileStorage = new StubFileStorage();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));
        byte[] jpegHeaderBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
        var file = CreateFormFile(jpegHeaderBytes, "photo.png", "image/png");

        var result = await controller.Upload(file, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<FileUploadResult>(okResult.Value);
        Assert.True(fileStorage.CommitAsyncCalled);
        Assert.Single(DbContext.Files);
    }

    [Fact]
    public async Task GetToken_ReturnsNotFound_ForAbsentFile()
    {
        var options = new BlazorBaseFileOptions();
        var controller = CreateController(options, new StubFileStorage(), PrincipalFor("user-1"));

        var result = await controller.GetToken(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetToken_ReturnsNotFound_ForNonOwningCaller()
    {
        var fileId = await SeedFileOwnedByAsync("user-1");
        var options = new BlazorBaseFileOptions();
        var controller = CreateController(options, new StubFileStorage(), PrincipalFor("user-2"));

        var result = await controller.GetToken(fileId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetToken_ReturnsToken_ForOwningCaller()
    {
        var fileId = await SeedFileOwnedByAsync("user-1");
        var options = new BlazorBaseFileOptions();
        var controller = CreateController(options, new StubFileStorage(), PrincipalFor("user-1"));

        var result = await controller.GetToken(fileId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var token = Assert.IsType<string>(okResult.Value);
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_ForAbsentFile()
    {
        var options = new BlazorBaseFileOptions();
        var controller = CreateController(options, new StubFileStorage(), PrincipalFor("user-1"));

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_ForNonOwningCaller()
    {
        var fileId = await SeedFileOwnedByAsync("user-1");
        var fileStorage = new StubFileStorage();
        var options = new BlazorBaseFileOptions();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-2"));

        var result = await controller.Delete(fileId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(fileStorage.DeleteAsyncCalled);
        Assert.NotEmpty(DbContext.Files);
    }

    [Fact]
    public async Task Delete_RemovesFile_ForOwningCaller()
    {
        var fileId = await SeedFileOwnedByAsync("user-1");
        var fileStorage = new StubFileStorage();
        var options = new BlazorBaseFileOptions();
        var controller = CreateController(options, fileStorage, PrincipalFor("user-1"));

        var result = await controller.Delete(fileId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(fileStorage.DeleteAsyncCalled);
        Assert.Empty(DbContext.Files);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
