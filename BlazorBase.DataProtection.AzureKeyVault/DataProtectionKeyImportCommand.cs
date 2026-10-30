using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.DataProtection.AzureKeyVault;

/// <summary>
/// Lets a host expose the one-time key import as a command line switch
/// (<c>--import-dataprotection-keys &lt;folder&gt;</c>) that runs the import and exits. The import never runs at
/// normal startup: without the switch <see cref="RunIfRequestedAsync{TDbContext}"/> does nothing and returns
/// <see langword="null"/>.
/// </summary>
public static class DataProtectionKeyImportCommand
{
    /// <summary>The name of the command line switch.</summary>
    public const string SwitchName = "--import-dataprotection-keys";

    /// <summary>The exit code returned when the import completed without skipped key files.</summary>
    public const int ExitCodeSuccess = 0;

    /// <summary>The exit code returned when key files were skipped (encrypted or invalid) or the import failed.</summary>
    public const int ExitCodeFailure = 1;

    /// <summary>
    /// Looks for the switch in <paramref name="args"/>. Supports <c>--import-dataprotection-keys &lt;folder&gt;</c> and
    /// <c>--import-dataprotection-keys=&lt;folder&gt;</c>.
    /// </summary>
    /// <param name="args">The command line arguments of the host.</param>
    /// <param name="folder">The folder given to the switch, or <see langword="null"/> when the folder is missing.</param>
    /// <returns><see langword="true"/> when the switch is present (even without a folder).</returns>
    public static bool IsRequested(string[] args, out string? folder)
    {
        ArgumentNullException.ThrowIfNull(args);

        folder = null;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (String.Equals(argument, SwitchName, StringComparison.OrdinalIgnoreCase))
            {
                var next = index + 1 < args.Length ? args[index + 1] : null;
                folder = String.IsNullOrWhiteSpace(next) || next.StartsWith("--", StringComparison.Ordinal) ? null : next;
                return true;
            }

            if (argument.StartsWith(SwitchName + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument[(SwitchName.Length + 1)..];
                folder = String.IsNullOrWhiteSpace(value) ? null : value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Runs the key import when <paramref name="args"/> contain the switch. Call it after the host is built and
    /// before it is started, and return the exit code from <c>Main</c> when it is not <see langword="null"/>:
    /// <c>if (await app.Services.RunIfRequestedAsync&lt;AppDbContext&gt;(args) is { } exitCode) return exitCode;</c>
    /// The database must already be migrated (the <c>DataProtectionKeys</c> table must exist).
    /// </summary>
    /// <typeparam name="TDbContext">The application's context that exposes the <c>DataProtectionKeys</c> table.</typeparam>
    /// <param name="services">The root service provider of the host.</param>
    /// <param name="args">The command line arguments of the host.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns><see langword="null"/> when the switch is absent (continue with the normal startup); otherwise the process exit code.</returns>
    public static async Task<int?> RunIfRequestedAsync<TDbContext>(this IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
        where TDbContext : DbContext, IDataProtectionKeyContext
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!IsRequested(args, out var folder))
            return null;

        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(DataProtectionKeyImportCommand).FullName!);

        if (folder == null)
        {
            logger?.LogError("{SwitchName} requires a folder argument, for example: {SwitchName} <folder>", SwitchName, SwitchName);
            return ExitCodeFailure;
        }

        try
        {
            var result = await services.ImportFromDirectoryAsync<TDbContext>(folder, cancellationToken);
            return result.Succeeded ? ExitCodeSuccess : ExitCodeFailure;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "The Data Protection key import from '{Folder}' failed.", folder);
            return ExitCodeFailure;
        }
    }
}
