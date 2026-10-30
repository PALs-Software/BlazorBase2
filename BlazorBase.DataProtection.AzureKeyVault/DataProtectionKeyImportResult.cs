namespace BlazorBase.DataProtection.AzureKeyVault;

/// <summary>
/// The outcome of importing a single key file. Never contains key material.
/// </summary>
public enum DataProtectionKeyImportOutcome
{
    /// <summary>The key was inserted into the <c>DataProtectionKeys</c> table.</summary>
    Imported,

    /// <summary>A key with the same key id already exists (in the database or in an earlier file of the same run).</summary>
    SkippedExisting,

    /// <summary>The file contains an encrypted key (for example Windows DPAPI or a certificate) and cannot be imported as is.</summary>
    SkippedEncrypted,

    /// <summary>The file is not a readable Data Protection key file.</summary>
    SkippedInvalid
}

/// <summary>
/// The outcome for one file of an import run.
/// </summary>
/// <param name="FileName">The file name (without directory).</param>
/// <param name="Outcome">What happened to the file.</param>
/// <param name="KeyId">The key id read from the file, or <see langword="null"/> when it could not be read.</param>
/// <param name="Message">A short, human readable explanation. Never contains key material.</param>
public sealed record DataProtectionKeyImportFileResult(string FileName, DataProtectionKeyImportOutcome Outcome, Guid? KeyId, string Message);

/// <summary>
/// The result of <see cref="DataProtectionKeyImporter"/>.
/// </summary>
public sealed class DataProtectionKeyImportResult
{
    private readonly List<DataProtectionKeyImportFileResult> _files = [];

    /// <summary>The per-file outcomes in processing order.</summary>
    public IReadOnlyList<DataProtectionKeyImportFileResult> Files => _files;

    /// <summary>The number of keys inserted into the database.</summary>
    public int Imported => Count(DataProtectionKeyImportOutcome.Imported);

    /// <summary>The number of keys skipped because their key id already exists.</summary>
    public int SkippedExisting => Count(DataProtectionKeyImportOutcome.SkippedExisting);

    /// <summary>The number of files skipped because the key is encrypted.</summary>
    public int SkippedEncrypted => Count(DataProtectionKeyImportOutcome.SkippedEncrypted);

    /// <summary>The number of files skipped because they are not valid key files.</summary>
    public int SkippedInvalid => Count(DataProtectionKeyImportOutcome.SkippedInvalid);

    /// <summary>The number of <c>revocation-*.xml</c> files found next to the keys. They are not imported.</summary>
    public int RevocationFilesIgnored { get; internal set; }

    /// <summary><see langword="true"/> when every key file was either imported or already present.</summary>
    public bool Succeeded => SkippedEncrypted == 0 && SkippedInvalid == 0;

    internal void Add(DataProtectionKeyImportFileResult file) => _files.Add(file);

    private int Count(DataProtectionKeyImportOutcome outcome) => _files.Count(file => file.Outcome == outcome);
}
