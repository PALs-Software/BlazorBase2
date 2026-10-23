using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Xunit;

namespace BlazorBase.Files.Server.AzureBlob.Test.Infrastructure;

/// <summary>
/// Locates the Azurite blob emulator used by the integration tests. The connection string comes from the
/// <c>BLAZORBASE_AZURITE_CONNECTION_STRING</c> environment variable and defaults to the emulator's standard
/// development storage endpoint. Tests skip themselves when the emulator cannot be reached.
/// </summary>
public static class AzuriteEnvironment
{
    public const string ConnectionStringVariable = "BLAZORBASE_AZURITE_CONNECTION_STRING";

    private static readonly Lazy<string?> UnavailableReason = new(Probe);

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } configured
            ? configured
            : "UseDevelopmentStorage=true";

    public static void SkipIfUnavailable() =>
        Skip.If(UnavailableReason.Value is not null, $"Azurite is not available ({UnavailableReason.Value}). Set {ConnectionStringVariable} or start Azurite with --skipApiVersionCheck.");

    private static string? Probe()
    {
        try
        {
            var options = new BlobClientOptions();
            options.Retry.MaxRetries = 0;
            options.Retry.NetworkTimeout = TimeSpan.FromSeconds(3);

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            new BlobServiceClient(ConnectionString, options).GetPropertiesAsync(cancellation.Token).GetAwaiter().GetResult();
            return null;
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }
}
