using BlazorBase.DataProtection.AzureKeyVault;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.DataProtection.Test;

public sealed class DataProtectionKeyImportCommandTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _temporaryDirectories = [];

    public DataProtectionKeyImportCommandTests()
    {
        _connection.Open();
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

    private ServiceProvider CreateHostServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<KeyDbContext>(options => options.UseSqlite(_connection));
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<KeyDbContext>().Database.EnsureCreated();
        return provider;
    }

    private string CreateKeyRingDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("BlazorBaseDataProtectionImportCommand").FullName;
        _temporaryDirectories.Add(directory);

        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("CommandTest").PersistKeysToFileSystem(new DirectoryInfo(directory));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Command").Protect("value");
        return directory;
    }

    [Theory]
    [InlineData(new string[0], false, null)]
    [InlineData(new[] { "--urls", "http://localhost" }, false, null)]
    [InlineData(new[] { "--import-dataprotection-keys", "C:\\keys" }, true, "C:\\keys")]
    [InlineData(new[] { "--IMPORT-DATAPROTECTION-KEYS", "/keys" }, true, "/keys")]
    [InlineData(new[] { "--import-dataprotection-keys=/keys" }, true, "/keys")]
    [InlineData(new[] { "--import-dataprotection-keys" }, true, null)]
    [InlineData(new[] { "--import-dataprotection-keys", "--other" }, true, null)]
    [InlineData(new[] { "--import-dataprotection-keys=" }, true, null)]
    public void IsRequested_ParsesTheSwitch(string[] args, bool expectedRequested, string? expectedFolder)
    {
        var requested = DataProtectionKeyImportCommand.IsRequested(args, out var folder);

        Assert.Equal(expectedRequested, requested);
        Assert.Equal(expectedFolder, folder);
    }

    [Fact]
    public async Task RunIfRequestedAsync_WithoutSwitch_ReturnsNullAndTouchesNothing()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        var exitCode = await services.RunIfRequestedAsync<KeyDbContext>(["--urls", "http://localhost"]);

        Assert.Null(exitCode);
    }

    [Fact]
    public async Task RunIfRequestedAsync_WithSwitchAndValidFolder_ImportsAndReturnsSuccess()
    {
        var directory = CreateKeyRingDirectory();
        await using var services = CreateHostServices();

        var exitCode = await services.RunIfRequestedAsync<KeyDbContext>(["--import-dataprotection-keys", directory]);

        Assert.Equal(DataProtectionKeyImportCommand.ExitCodeSuccess, exitCode);
        using var scope = services.CreateScope();
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<KeyDbContext>().DataProtectionKeys.Count());
    }

    [Fact]
    public async Task RunIfRequestedAsync_SwitchWithoutFolder_ReturnsFailure()
    {
        await using var services = CreateHostServices();

        var exitCode = await services.RunIfRequestedAsync<KeyDbContext>(["--import-dataprotection-keys"]);

        Assert.Equal(DataProtectionKeyImportCommand.ExitCodeFailure, exitCode);
    }

    [Fact]
    public async Task RunIfRequestedAsync_MissingFolder_ReturnsFailure()
    {
        await using var services = CreateHostServices();

        var exitCode = await services.RunIfRequestedAsync<KeyDbContext>(["--import-dataprotection-keys", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))]);

        Assert.Equal(DataProtectionKeyImportCommand.ExitCodeFailure, exitCode);
    }

    [Fact]
    public async Task RunIfRequestedAsync_FolderWithSkippedFile_ReturnsFailureButKeepsImportedKeys()
    {
        var directory = CreateKeyRingDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "key-bad.xml"), "garbage");
        await using var services = CreateHostServices();

        var exitCode = await services.RunIfRequestedAsync<KeyDbContext>(["--import-dataprotection-keys", directory]);

        Assert.Equal(DataProtectionKeyImportCommand.ExitCodeFailure, exitCode);
        using var scope = services.CreateScope();
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<KeyDbContext>().DataProtectionKeys.Count());
    }
}
