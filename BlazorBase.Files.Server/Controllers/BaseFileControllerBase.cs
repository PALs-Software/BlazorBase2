using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Files.Server.Controllers;

/// <summary>
/// Abstract ASP.NET Core controller base providing upload, download, thumbnail, token-issuance,
/// and delete endpoints for <see cref="BaseFile"/> entities. The host derives a thin concrete
/// class and adds the <c>[ApiController]</c> and <c>[Route]</c> attributes so ASP.NET Core
/// discovers it via controller scanning.
/// </summary>
/// <remarks>
/// DB access uses <see cref="DbContext.Set{TEntity}()"/> so the base never references a
/// concrete application context. The host's <c>DbContext</c> must expose a
/// <c>DbSet&lt;BaseFile&gt;</c>, and a scoped <see cref="DbContext"/> must be resolvable from the
/// container — <c>AddBlazorBaseFilesServer&lt;TContext&gt;()</c> bridges this automatically from
/// the host's <c>AddDbContext&lt;TContext&gt;()</c> registration, so no separate registration is
/// needed.
/// </remarks>
[ApiController]
[Route("api/files")]
[Authorize]
public abstract class BaseFileControllerBase(
    IFileStorage fileStorage,
    IImageService imageService,
    IFileAccessAuthorizer fileAccessAuthorizer,
    IFileAccessTokenService fileAccessTokenService,
    IBlazorBaseFileOptions options,
    DbContext dbContext) : ControllerBase
{
    #region Injects
    private readonly IFileStorage FileStorage = fileStorage;
    private readonly IImageService ImageService = imageService;
    private readonly IFileAccessAuthorizer FileAccessAuthorizer = fileAccessAuthorizer;
    private readonly IFileAccessTokenService FileAccessTokenService = fileAccessTokenService;
    private readonly IBlazorBaseFileOptions Options = options;
    private readonly DbContext DbContext = dbContext;
    #endregion

    private const string ThumbnailContentType = "image/jpeg";
    private const string DownloadFallbackContentType = "application/octet-stream";
    private const int ContentSignatureHeaderLength = 16;
    private const int MaxOwnerScopeKeyLength = 200;
    private const int MaxExtensionLength = 20;
    private const int MaxFileNameLength = 260;
    private const int MaxContentTypeLength = 150;

    /// <summary>
    /// Uploads a file: validates size and content type server-side, streams to temporary storage,
    /// generates a thumbnail for images, computes SHA-256, persists a <see cref="BaseFile"/> row,
    /// and returns a <see cref="FileUploadResult"/>.
    /// </summary>
    [HttpPost]
    public virtual async Task<ActionResult<FileUploadResult>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? ownerScopeKey,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            return BadRequest("No file content received.");

        if ((long)Options.MaxFileSizeBytes > 0 && file.Length > (long)Options.MaxFileSizeBytes)
            return BadRequest($"File exceeds the maximum allowed size of {Options.MaxFileSizeBytes} bytes.");

        if (!IsContentTypeAllowed(file.ContentType))
            return BadRequest($"Content type '{file.ContentType}' is not permitted.");

        if (ownerScopeKey is { Length: > MaxOwnerScopeKeyLength })
            return BadRequest($"ownerScopeKey exceeds the maximum length of {MaxOwnerScopeKeyLength} characters.");

        if (!await FileAccessAuthorizer.CanAssignScopeAsync(ownerScopeKey, User, cancellationToken).ConfigureAwait(false))
            return Forbid();

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension.Length > MaxExtensionLength)
            return BadRequest($"File extension exceeds the maximum length of {MaxExtensionLength} characters.");

        if (Path.GetFileNameWithoutExtension(file.FileName).Length > MaxFileNameLength)
            return BadRequest($"File name exceeds the maximum length of {MaxFileNameLength} characters.");

        if (file.ContentType.Length > MaxContentTypeLength)
            return BadRequest($"Content type exceeds the maximum length of {MaxContentTypeLength} characters.");

        await using var sourceStream = file.OpenReadStream();

        if (Options.ValidateContentTypeSignature && FileContentSignatureValidator.CanValidate(file.ContentType))
        {
            var headerBuffer = new byte[ContentSignatureHeaderLength];
            var headerBytesRead = await ReadHeaderAsync(sourceStream, headerBuffer, cancellationToken).ConfigureAwait(false);
            sourceStream.Seek(0, SeekOrigin.Begin);

            if (!FileContentSignatureValidator.IsContentConsistentWithSignature(headerBuffer.AsSpan(0, headerBytesRead), file.ContentType))
                return BadRequest($"File content does not match the declared content type '{file.ContentType}'.");
        }

        var storedFile = await FileStorage.SaveTemporaryAsync(sourceStream, extension, cancellationToken).ConfigureAwait(false);

        Stream? thumbnailStream = null;

        if (Options.UseImageThumbnails && IsImageContentType(file.ContentType))
        {
            sourceStream.Seek(0, SeekOrigin.Begin);

            try
            {
                thumbnailStream = await CreateThumbnailStreamAsync(sourceStream, cancellationToken).ConfigureAwait(false);
            }
            catch (ImageTooLargeException)
            {
                return BadRequest("The uploaded image exceeds the maximum permitted dimensions.");
            }
            catch (SixLabors.ImageSharp.ImageFormatException)
            {
                return BadRequest("The uploaded file could not be processed as a valid image.");
            }
        }

        var permanentFileId = Guid.NewGuid();
        await FileStorage.CommitAsync(permanentFileId, storedFile.FileId, extension, thumbnailStream, cancellationToken).ConfigureAwait(false);

        if (thumbnailStream is not null)
            await thumbnailStream.DisposeAsync().ConfigureAwait(false);

        var baseFile = new BaseFile
        {
            Id = permanentFileId,
            FileName = Path.GetFileNameWithoutExtension(file.FileName),
            Extension = extension,
            ContentType = file.ContentType,
            FileSize = storedFile.FileSize,
            Hash = storedFile.Hash,
            OwnerScopeKey = ownerScopeKey ?? FileAccessAuthorizer.ResolveDefaultOwnerScope(User),
        };

        DbContext.Set<BaseFile>().Add(baseFile);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new FileUploadResult(
            baseFile.Id,
            baseFile.FileName,
            baseFile.Extension,
            baseFile.ContentType,
            baseFile.FileSize,
            baseFile.Hash));
    }

    /// <summary>
    /// Issues a short-lived signed token for the file identified by <paramref name="id"/>.
    /// Requires bearer authentication. The token is passed as a query parameter to the
    /// <see cref="Download"/> and <see cref="Thumbnail"/> endpoints. An unauthorized caller
    /// receives the same <see cref="NotFoundResult"/> as a genuinely absent file, so an
    /// authenticated non-owner cannot enumerate valid file identifiers by observing a 403-vs-404
    /// status difference.
    /// </summary>
    [HttpGet("{id:guid}/token")]
    public virtual async Task<ActionResult<string>> GetToken(Guid id, CancellationToken cancellationToken)
    {
        if (!await FileAccessAuthorizer.CanAccessAsync(id, User, cancellationToken).ConfigureAwait(false))
            return NotFound();

        return Ok(FileAccessTokenService.IssueToken(id));
    }

    /// <summary>
    /// Downloads the file identified by <paramref name="id"/>. Range requests are supported
    /// (<c>enableRangeProcessing: true</c>). Access is gated by a signed token query parameter
    /// (<c>?token=…</c>), which is only issued to a caller that already passed
    /// <see cref="IFileAccessAuthorizer.CanAccessAsync"/> in <see cref="GetToken"/> — the token
    /// itself is the capability proof, so it is also honored for anonymous recipients the owner
    /// shared a link with.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> Download(Guid id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        if (!FileAccessTokenService.ValidateToken(id, token))
            return Unauthorized();

        var file = await DbContext.Set<BaseFile>().FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (file is null)
            return NotFound();

        var readResult = await FileStorage.OpenReadAsync(id, thumbnail: false, file.Extension, cancellationToken).ConfigureAwait(false);
        if (readResult is null)
            return NotFound();

        ApplyContentHardeningHeaders();

        return File(readResult.Content, file.ContentType, file.FileName + file.Extension, enableRangeProcessing: true);
    }

    /// <summary>
    /// Returns the thumbnail for the file identified by <paramref name="id"/>. Falls back to
    /// the original file when no thumbnail exists. For non-image files, returns the original.
    /// Access is gated by a signed token query parameter (<c>?token=…</c>), which is only issued
    /// to a caller that already passed <see cref="IFileAccessAuthorizer.CanAccessAsync"/> in
    /// <see cref="GetToken"/> — the token itself is the capability proof, so it is also honored
    /// for anonymous recipients the owner shared a link with.
    /// </summary>
    [HttpGet("{id:guid}/thumbnail")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> Thumbnail(Guid id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        if (!FileAccessTokenService.ValidateToken(id, token))
            return Unauthorized();

        var file = await DbContext.Set<BaseFile>().FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (file is null)
            return NotFound();

        var isImage = file.IsImage();
        var readResult = await FileStorage.OpenReadAsync(id, thumbnail: isImage, file.Extension, cancellationToken).ConfigureAwait(false);
        if (readResult is null)
            return NotFound();

        ApplyContentHardeningHeaders();

        if (!isImage)
            return File(readResult.Content, DownloadFallbackContentType, file.FileName + file.Extension, enableRangeProcessing: false);

        return File(readResult.Content, ThumbnailContentType, enableRangeProcessing: false);
    }

    /// <summary>
    /// Deletes the file row and its stored content. Requires bearer authentication. An
    /// unauthorized caller receives the same <see cref="NotFoundResult"/> as a genuinely absent
    /// file, so an authenticated non-owner cannot enumerate valid file identifiers by observing a
    /// 403-vs-404 status difference.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await FileAccessAuthorizer.CanAccessAsync(id, User, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var file = await DbContext.Set<BaseFile>().FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (file is null)
            return NotFound();

        DbContext.Set<BaseFile>().Remove(file);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await FileStorage.DeleteAsync(id, file.Extension, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    private bool IsContentTypeAllowed(string contentType)
    {
        if (Options.AllowedContentTypes is not { Length: > 0 })
            return true;

        foreach (var allowed in Options.AllowedContentTypes)
        {
            if (allowed.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = allowed[..^1];
                if (contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (allowed.Equals(contentType, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsImageContentType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] headerBuffer, CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < headerBuffer.Length)
        {
            var bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(totalBytesRead), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                break;

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    private void ApplyContentHardeningHeaders()
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
    }

    private async Task<MemoryStream> CreateThumbnailStreamAsync(Stream sourceStream, CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        var tempThumbPath = Path.GetTempFileName();

        try
        {
            await ImageService.CreateThumbnailAsync(sourceStream, Options.ImageThumbnailSize, tempThumbPath, cancellationToken).ConfigureAwait(false);
            var thumbBytes = await System.IO.File.ReadAllBytesAsync(tempThumbPath, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(thumbBytes, cancellationToken).ConfigureAwait(false);
            output.Seek(0, SeekOrigin.Begin);
        }
        finally
        {
            System.IO.File.Delete(tempThumbPath);
        }

        return output;
    }
}
