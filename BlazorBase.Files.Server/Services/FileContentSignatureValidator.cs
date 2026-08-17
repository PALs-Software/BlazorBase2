namespace BlazorBase.Files.Server.Services;

/// <summary>
/// Validates that an uploaded file's leading bytes ("magic numbers") match its declared MIME
/// content type. Content types without a reliable, universally recognized signature (for example
/// text-based formats) cannot be verified this way — <see cref="CanValidate"/> reports this so
/// callers treat an unsniffable type as "cannot verify, allow" rather than rejecting it.
/// </summary>
public static class FileContentSignatureValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Gif87Signature = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89Signature = "GIF89a"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebPSignature = "WEBP"u8.ToArray();
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="declaredContentType"/> has a known,
    /// reliable magic-byte signature that <see cref="IsContentConsistentWithSignature"/> can check.
    /// </summary>
    public static bool CanValidate(string declaredContentType) =>
        NormalizeContentType(declaredContentType) is "image/jpeg" or "image/png" or "image/gif"
            or "image/webp" or "image/bmp" or "image/x-ms-bmp" or "application/pdf";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="header"/> (the leading bytes of the
    /// uploaded content) matches a known signature for <paramref name="declaredContentType"/>.
    /// Callers must check <see cref="CanValidate"/> first — an unsniffable content type always
    /// returns <see langword="false"/> here and must be treated as unverifiable rather than
    /// rejected.
    /// </summary>
    public static bool IsContentConsistentWithSignature(ReadOnlySpan<byte> header, string declaredContentType)
    {
        var normalizedContentType = NormalizeContentType(declaredContentType);

        return normalizedContentType switch
        {
            "image/jpeg" => MatchesJpegSignature(header),
            "image/png" => header.Length >= PngSignature.Length && header[..PngSignature.Length].SequenceEqual(PngSignature),
            "image/gif" => MatchesGifSignature(header),
            "image/webp" => MatchesWebPSignature(header),
            "image/bmp" or "image/x-ms-bmp" => header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D,
            "application/pdf" => header.Length >= PdfSignature.Length && header[..PdfSignature.Length].SequenceEqual(PdfSignature),
            _ => false,
        };
    }

    private static bool MatchesJpegSignature(ReadOnlySpan<byte> header) =>
        header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

    private static bool MatchesGifSignature(ReadOnlySpan<byte> header)
    {
        if (header.Length < 6)
            return false;

        var candidate = header[..6];
        return candidate.SequenceEqual(Gif87Signature) || candidate.SequenceEqual(Gif89Signature);
    }

    private static bool MatchesWebPSignature(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12)
            return false;

        return header[..4].SequenceEqual(RiffSignature) && header[8..12].SequenceEqual(WebPSignature);
    }

    private static string NormalizeContentType(string declaredContentType)
    {
        var parameterIndex = declaredContentType.IndexOf(';');
        var mediaType = parameterIndex >= 0 ? declaredContentType[..parameterIndex] : declaredContentType;
        return mediaType.Trim().ToLowerInvariant();
    }
}
