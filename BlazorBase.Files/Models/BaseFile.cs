using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BlazorBase.CRUD.Core;

namespace BlazorBase.Files.Models;

/// <summary>
/// Pure metadata record for a stored file. Holds identity, classification, and integrity data;
/// all I/O is the responsibility of the server-side storage service (<c>BlazorBase.Files.Server</c>).
/// The host application owns the <c>DbSet&lt;BaseFile&gt;</c> and migration.
/// </summary>
public class BaseFile : AuditModel, IBaseFile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Extension { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(64)]
    public string? Hash { get; set; }

    [MaxLength(200)]
    public string? OwnerScopeKey { get; set; }

    /// <inheritdoc/>
    public bool IsImage() => ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string GetDownloadUrl(string route)
    {
        var baseUrl = $"/{route.TrimStart('/')}/{Id}";
        return Hash is null ? baseUrl : $"{baseUrl}?h={Hash}";
    }

    /// <inheritdoc/>
    public string GetDownloadUrl(string route, string? token)
    {
        var url = GetDownloadUrl(route);

        if (string.IsNullOrEmpty(token))
            return url;

        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}token={Uri.EscapeDataString(token)}";
    }

    /// <inheritdoc/>
    public string GetThumbnailUrl(string route)
    {
        var baseUrl = $"/{route.TrimStart('/')}/{Id}/thumbnail";
        return Hash is null ? baseUrl : $"{baseUrl}?h={Hash}";
    }

    /// <inheritdoc/>
    public string GetThumbnailUrl(string route, string? token)
    {
        var url = GetThumbnailUrl(route);

        if (string.IsNullOrEmpty(token))
            return url;

        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}token={Uri.EscapeDataString(token)}";
    }
}
