using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml;
using System.Xml.Linq;

namespace BlazorBase.DataProtection.AzureKeyVault;

/// <summary>
/// One-time import of an existing file-system Data Protection key ring (<c>key-*.xml</c>) into the
/// <c>DataProtectionKeys</c> table of the application database, so that data protected before moving to the
/// database backed key ring stays readable. The import is idempotent: keys whose key id already exists are skipped.
/// Only plain (unencrypted) key files are imported; key files that are encrypted at rest are skipped and reported.
/// Key material is never logged.
/// </summary>
public static class DataProtectionKeyImporter
{
    private const string KeyFilePattern = "key-*.xml";
    private const string RevocationFilePattern = "revocation-*.xml";
    private static readonly string[] EncryptedElementNames = ["encryptedSecret", "EncryptedData", "EncryptedKey"];

    /// <summary>
    /// Imports the key files of <paramref name="directoryPath"/> using a new scope of <paramref name="services"/>.
    /// Resolves <typeparamref name="TDbContext"/> and, when registered, an <see cref="ILoggerFactory"/> from that scope.
    /// </summary>
    /// <typeparam name="TDbContext">The application's context that exposes the <c>DataProtectionKeys</c> table.</typeparam>
    /// <param name="services">The root service provider of the host.</param>
    /// <param name="directoryPath">The folder of the old file-system key ring.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    public static async Task<DataProtectionKeyImportResult> ImportFromDirectoryAsync<TDbContext>(this IServiceProvider services, string directoryPath, CancellationToken cancellationToken = default)
        where TDbContext : DbContext, IDataProtectionKeyContext
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(DataProtectionKeyImporter).FullName!);

        return await ImportFromDirectoryAsync(dbContext, directoryPath, logger, cancellationToken);
    }

    /// <summary>
    /// Imports the key files of <paramref name="directoryPath"/> into <paramref name="dbContext"/>. Valid new keys are
    /// saved together in one <c>SaveChangesAsync</c> call. <c>revocation-*.xml</c> files are not imported.
    /// </summary>
    /// <typeparam name="TDbContext">The application's context that exposes the <c>DataProtectionKeys</c> table.</typeparam>
    /// <param name="dbContext">The context to insert the keys into.</param>
    /// <param name="directoryPath">The folder of the old file-system key ring.</param>
    /// <param name="logger">Optional logger for counts and per-file outcomes (no key material).</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    public static async Task<DataProtectionKeyImportResult> ImportFromDirectoryAsync<TDbContext>(TDbContext dbContext, string directoryPath, ILogger? logger = null, CancellationToken cancellationToken = default)
        where TDbContext : DbContext, IDataProtectionKeyContext
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        logger ??= NullLogger.Instance;

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"The Data Protection key folder '{directoryPath}' does not exist.");

        var result = new DataProtectionKeyImportResult();
        var keyFiles = Directory.EnumerateFiles(directoryPath, KeyFilePattern).Order(StringComparer.OrdinalIgnoreCase).ToList();
        result.RevocationFilesIgnored = Directory.EnumerateFiles(directoryPath, RevocationFilePattern).Count();

        if (keyFiles.Count == 0)
            logger.LogWarning("No '{Pattern}' files found in '{Directory}'. Nothing to import.", KeyFilePattern, directoryPath);

        if (result.RevocationFilesIgnored > 0)
            logger.LogWarning("{Count} revocation file(s) found in '{Directory}' are not imported. Keys revoked in the old key ring stay active after the import.", result.RevocationFilesIgnored, directoryPath);

        var knownKeyIds = await LoadExistingKeyIdsAsync(dbContext, cancellationToken);

        foreach (var keyFile in keyFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(keyFile);
            var inspection = Inspect(keyFile);

            if (inspection.Outcome != DataProtectionKeyImportOutcome.Imported)
            {
                Report(result, logger, new DataProtectionKeyImportFileResult(fileName, inspection.Outcome, inspection.KeyId, inspection.Message));
                continue;
            }

            if (!knownKeyIds.Add(inspection.KeyId!.Value))
            {
                Report(result, logger, new DataProtectionKeyImportFileResult(fileName, DataProtectionKeyImportOutcome.SkippedExisting, inspection.KeyId, "A key with this key id already exists."));
                continue;
            }

            dbContext.DataProtectionKeys.Add(new DataProtectionKey
            {
                FriendlyName = Path.GetFileNameWithoutExtension(keyFile),
                Xml = inspection.Xml
            });

            Report(result, logger, new DataProtectionKeyImportFileResult(fileName, DataProtectionKeyImportOutcome.Imported, inspection.KeyId, "Key imported."));
        }

        if (result.Imported > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Data Protection key import from '{Directory}' finished: {Imported} imported, {SkippedExisting} already present, {SkippedEncrypted} encrypted (skipped), {SkippedInvalid} invalid (skipped).",
            directoryPath, result.Imported, result.SkippedExisting, result.SkippedEncrypted, result.SkippedInvalid);

        return result;
    }

    private static async Task<HashSet<Guid>> LoadExistingKeyIdsAsync<TDbContext>(TDbContext dbContext, CancellationToken cancellationToken)
        where TDbContext : DbContext, IDataProtectionKeyContext
    {
        var existingXml = await dbContext.DataProtectionKeys.AsNoTracking().Select(key => key.Xml).ToListAsync(cancellationToken);

        var keyIds = new HashSet<Guid>();
        foreach (var xml in existingXml)
        {
            if (String.IsNullOrWhiteSpace(xml))
                continue;

            try
            {
                var root = XElement.Parse(xml);
                if (TryReadKeyId(root, out var keyId))
                    keyIds.Add(keyId);
            }
            catch (XmlException)
            {
            }
        }

        return keyIds;
    }

    private static Inspection Inspect(string filePath)
    {
        XElement root;
        try
        {
            using var reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            root = XDocument.Load(reader).Root ?? throw new XmlException("The document has no root element.");
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
        {
            return new Inspection(DataProtectionKeyImportOutcome.SkippedInvalid, null, "The file is not readable well-formed XML.", null);
        }

        var hasKeyId = TryReadKeyId(root, out var keyId);
        Guid? nullableKeyId = hasKeyId ? keyId : null;

        if (root.Name.LocalName != "key" || !hasKeyId)
            return new Inspection(DataProtectionKeyImportOutcome.SkippedInvalid, nullableKeyId, "The file is not a Data Protection key (root element 'key' with a key id is missing).", null);

        if (root.Descendants().Any(element => EncryptedElementNames.Contains(element.Name.LocalName)))
            return new Inspection(DataProtectionKeyImportOutcome.SkippedEncrypted, nullableKeyId, "The key is encrypted at rest (for example Windows DPAPI or a certificate). Decrypt it on the source machine first; it is not imported.", null);

        var masterKeyValue = root.Descendants().FirstOrDefault(element => element.Name.LocalName == "masterKey")?.Elements().FirstOrDefault(element => element.Name.LocalName == "value")?.Value;
        if (String.IsNullOrWhiteSpace(masterKeyValue))
            return new Inspection(DataProtectionKeyImportOutcome.SkippedInvalid, nullableKeyId, "The key file contains no plain master key value.", null);

        return new Inspection(DataProtectionKeyImportOutcome.Imported, nullableKeyId, "Key can be imported.", root.ToString(SaveOptions.DisableFormatting));
    }

    private static bool TryReadKeyId(XElement root, out Guid keyId)
    {
        keyId = Guid.Empty;
        return root.Name.LocalName == "key" && Guid.TryParse(root.Attribute("id")?.Value, out keyId);
    }

    private static void Report(DataProtectionKeyImportResult result, ILogger logger, DataProtectionKeyImportFileResult file)
    {
        result.Add(file);

        switch (file.Outcome)
        {
            case DataProtectionKeyImportOutcome.Imported:
                logger.LogInformation("Imported Data Protection key {KeyId} from '{FileName}'.", file.KeyId, file.FileName);
                break;
            case DataProtectionKeyImportOutcome.SkippedExisting:
                logger.LogInformation("Skipped '{FileName}': key {KeyId} already exists.", file.FileName, file.KeyId);
                break;
            case DataProtectionKeyImportOutcome.SkippedEncrypted:
                logger.LogError("Skipped '{FileName}' (key {KeyId}): {Message}", file.FileName, file.KeyId, file.Message);
                break;
            default:
                logger.LogError("Skipped '{FileName}': {Message}", file.FileName, file.Message);
                break;
        }
    }

    private sealed record Inspection(DataProtectionKeyImportOutcome Outcome, Guid? KeyId, string Message, string? Xml);
}
