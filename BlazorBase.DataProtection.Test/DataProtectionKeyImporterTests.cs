using Azure.Core;
using BlazorBase.DataProtection.AzureKeyVault;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Xunit;

namespace BlazorBase.DataProtection.Test;

public sealed class DataProtectionKeyImporterTests : IDisposable
{
    private const string ApplicationName = "BlazorBase.DataProtection.Test.Import";
    private const string Purpose = "Import.Purpose";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _temporaryDirectories = [];

    public DataProtectionKeyImporterTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        foreach (var directory in _temporaryDirectories)
            Directory.Delete(directory, recursive: true);
    }

    private sealed class KeyDbContext(DbContextOptions<KeyDbContext> options) : DbContext(options), IDataProtectionKeyContext
    {
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    }

    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("stub", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private KeyDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<KeyDbContext>().UseSqlite(_connection).Options);

    private string CreateTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("BlazorBaseDataProtectionImport").FullName;
        _temporaryDirectories.Add(directory);
        return directory;
    }

    private static string CreateFileSystemKeyRing(string directory, string applicationName, string protectedValue, out string cipherText, Action<IDataProtectionBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        var builder = services.AddDataProtection().SetApplicationName(applicationName).PersistKeysToFileSystem(new DirectoryInfo(directory));
        configure?.Invoke(builder);

        using var provider = services.BuildServiceProvider();
        cipherText = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect(protectedValue);
        return directory;
    }

    private static string KeyFileOf(string directory) => Directory.GetFiles(directory, "key-*.xml").Single();

    private static IServiceProvider CreateDatabaseBackedProvider(SqliteConnection connection, string applicationName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<KeyDbContext>(options => options.UseSqlite(connection));
        services.AddDataProtection().SetApplicationName(applicationName).PersistKeysToDbContext<KeyDbContext>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_PlainKeyFile_InsertsKeyWithFileNameAndOriginalKeyId()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _);
        var keyFile = KeyFileOf(directory);
        var expectedKeyId = Guid.Parse(XElement.Load(keyFile).Attribute("id")!.Value);

        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Imported);
        var stored = Assert.Single(context.DataProtectionKeys.AsNoTracking().ToList());
        Assert.Equal(Path.GetFileNameWithoutExtension(keyFile), stored.FriendlyName);
        Assert.Equal(expectedKeyId, Guid.Parse(XElement.Parse(stored.Xml!).Attribute("id")!.Value));
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_RunTwice_IsIdempotent()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _);

        using var context = CreateContext();
        var first = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);
        var second = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.Equal(1, first.Imported);
        Assert.Equal(0, second.Imported);
        Assert.Equal(1, second.SkippedExisting);
        Assert.True(second.Succeeded);
        Assert.Equal(1, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_KeyAlreadyInDatabaseWithDifferentFriendlyName_IsSkippedByKeyId()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _);
        var keyXml = XElement.Load(KeyFileOf(directory)).ToString(SaveOptions.DisableFormatting);

        using var context = CreateContext();
        context.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "something-else", Xml = keyXml });
        await context.SaveChangesAsync();

        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.SkippedExisting);
        Assert.Equal(1, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_SameKeyIdInTwoFiles_ImportsOnlyOnce()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _);
        File.Copy(KeyFileOf(directory), Path.Combine(directory, "key-copy.xml"));

        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.SkippedExisting);
        Assert.Equal(1, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_DpapiStyleEncryptedKey_IsSkippedAndReported()
    {
        var directory = CreateTemporaryDirectory();
        var keyId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, $"key-{keyId}.xml"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <key id="{keyId}" version="1">
              <creationDate>2026-01-01T00:00:00Z</creationDate>
              <activationDate>2026-01-01T00:00:00Z</activationDate>
              <expirationDate>2026-04-01T00:00:00Z</expirationDate>
              <descriptor deserializerType="Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel.AuthenticatedEncryptorDescriptorDeserializer, Microsoft.AspNetCore.DataProtection, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60">
                <descriptor>
                  <encryption algorithm="AES_256_CBC" />
                  <validation algorithm="HMACSHA256" />
                  <masterKey p4:requiresEncryption="true" xmlns:p4="http://schemas.asp.net/2015/03/dataProtection">
                    <encryptedSecret decryptorType="Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlDecryptor, Microsoft.AspNetCore.DataProtection, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60" xmlns="http://schemas.asp.net/2015/03/dataProtection">
                      <value>AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA</value>
                    </encryptedSecret>
                  </masterKey>
                </descriptor>
              </descriptor>
            </key>
            """);

        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.SkippedEncrypted);
        var file = Assert.Single(result.Files);
        Assert.Equal(keyId, file.KeyId);
        Assert.Equal(0, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_CertificateEncryptedKeyFromRealKeyRing_IsSkippedAndReported()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=BlazorBase.DataProtection.Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _, builder => builder.ProtectKeysWithCertificate(certificate));

        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.Equal(1, result.SkippedEncrypted);
        Assert.Equal(0, result.Imported);
        Assert.Equal(0, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Theory]
    [InlineData("this is not xml")]
    [InlineData("<other id=\"11111111-1111-1111-1111-111111111111\" />")]
    [InlineData("<key version=\"1\" />")]
    [InlineData("<key id=\"not-a-guid\" version=\"1\" />")]
    [InlineData("<key id=\"11111111-1111-1111-1111-111111111111\" version=\"1\"><descriptor /></key>")]
    [InlineData("<!DOCTYPE key [<!ENTITY x \"y\">]><key id=\"11111111-1111-1111-1111-111111111111\" />")]
    public async Task ImportFromDirectoryAsync_MalformedFile_IsSkippedAsInvalid(string content)
    {
        var directory = CreateTemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "key-bad.xml"), content);

        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.SkippedInvalid);
        Assert.Equal(0, result.Imported);
        Assert.Equal(0, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_MixedFolder_ImportsValidKeysAndReportsTheRest()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _);
        await File.WriteAllTextAsync(Path.Combine(directory, "key-bad.xml"), "garbage");
        await File.WriteAllTextAsync(Path.Combine(directory, "revocation-1.xml"), "<revocation version=\"1\" />");
        await File.WriteAllTextAsync(Path.Combine(directory, "readme.txt"), "ignored");

        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.SkippedInvalid);
        Assert.Equal(1, result.RevocationFilesIgnored);
        Assert.False(result.Succeeded);
        Assert.Equal(1, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_EmptyDirectory_ReturnsEmptySuccessfulResult()
    {
        using var context = CreateContext();
        var result = await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, CreateTemporaryDirectory());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_MissingDirectory_Throws()
    {
        using var context = CreateContext();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            DataProtectionKeyImporter.ImportFromDirectoryAsync(context, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_Logs_CountsButNeverKeyMaterial()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "value", out _);
        var masterKey = XElement.Load(KeyFileOf(directory)).Descendants().Single(element => element.Name.LocalName == "masterKey").Elements().Single(element => element.Name.LocalName == "value").Value;
        var logger = new ListLogger();

        using var context = CreateContext();
        await DataProtectionKeyImporter.ImportFromDirectoryAsync(context, directory, logger);

        Assert.Contains(logger.Messages, message => message.Contains("1 imported"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(masterKey));
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_FileSystemKeyRing_UnprotectsWithDatabaseBackedKeyRing()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "top secret", out var cipherText);

        using var provider = (ServiceProvider)CreateDatabaseBackedProvider(_connection, ApplicationName);
        var result = await provider.ImportFromDirectoryAsync<KeyDbContext>(directory);

        var unprotected = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(cipherText);

        Assert.Equal(1, result.Imported);
        Assert.Equal("top secret", unprotected);
        using var context = CreateContext();
        Assert.Equal(1, context.DataProtectionKeys.AsNoTracking().Count());
    }

    [Fact]
    public async Task ImportFromDirectoryAsync_FileSystemKeyRing_IsNotReadableWithOtherApplicationName()
    {
        var directory = CreateFileSystemKeyRing(CreateTemporaryDirectory(), ApplicationName, "top secret", out var cipherText);

        using var provider = (ServiceProvider)CreateDatabaseBackedProvider(_connection, ApplicationName + ".Other");
        await provider.ImportFromDirectoryAsync<KeyDbContext>(directory);

        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(cipherText));
    }

    [Fact]
    public void AddBlazorBaseAzureKeyVaultDataProtection_PersistsKeysInDbContextAndWrapsThemWithKeyVault()
    {
        var services = new ServiceCollection();
        services.AddDbContext<KeyDbContext>(options => options.UseSqlite(_connection));
        services.AddBlazorBaseAzureKeyVaultDataProtection<KeyDbContext>(settings =>
        {
            settings.ApplicationName = ApplicationName;
            settings.AzureKeyVaultKeyIdentifier = new Uri("https://example-vault.vault.azure.net/keys/dataprotection-key");
            settings.TokenCredential = new StubCredential();
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>().Value;

        Assert.IsType<EntityFrameworkCoreXmlRepository<KeyDbContext>>(options.XmlRepository);
        Assert.NotNull(options.XmlEncryptor);
        Assert.Contains("AzureKeyVault", options.XmlEncryptor.GetType().Name);
    }
}
