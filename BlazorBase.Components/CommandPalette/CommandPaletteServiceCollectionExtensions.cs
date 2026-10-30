using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Components.CommandPalette;

/// <summary>Registration of the command palette and its providers; safe for Blazor Server and WebAssembly.</summary>
public static class CommandPaletteServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICommandPaletteService"/> and the <see cref="CommandPaletteOptions"/>. Calling it more than
    /// once is safe; every call may add configuration. <c>AddBlazorBaseComponents</c> calls it already.
    /// </summary>
    public static IServiceCollection AddBlazorBaseCommandPalette(this IServiceCollection services, Action<CommandPaletteOptions>? configure = null)
    {
        services.TryAddScoped<ICommandPaletteService, CommandPaletteService>();
        var options = services.AddOptions<CommandPaletteOptions>();
        if (configure is not null)
            options.Configure(configure);

        return services;
    }

    /// <summary>Registers a provider class, created per scope (per circuit on the server, once in the browser).</summary>
    public static IServiceCollection AddCommandPaletteProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ICommandPaletteProvider
    {
        services.AddBlazorBaseCommandPalette();
        services.AddScoped<ICommandPaletteProvider, TProvider>();
        return services;
    }

    /// <summary>Registers a provider built by a factory, for example one that needs the localizer of its own resources.</summary>
    public static IServiceCollection AddCommandPaletteProvider(this IServiceCollection services, Func<IServiceProvider, ICommandPaletteProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        services.AddBlazorBaseCommandPalette();
        services.AddScoped(factory);
        return services;
    }

    /// <summary>Registers a provider that is a single function of the request.</summary>
    public static IServiceCollection AddCommandPaletteProvider(
        this IServiceCollection services,
        Func<CommandPaletteRequest, CancellationToken, ValueTask<IReadOnlyList<CommandPaletteEntry>>> search)
    {
        ArgumentNullException.ThrowIfNull(search);
        return services.AddCommandPaletteProvider(_ => new DelegateCommandPaletteProvider(search));
    }

    /// <summary>Registers a fixed list of entries; the palette filters and ranks it like any other provider's result.</summary>
    public static IServiceCollection AddCommandPaletteProvider(this IServiceCollection services, IReadOnlyList<CommandPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return services.AddCommandPaletteProvider(_ => new StaticCommandPaletteProvider(entries));
    }
}

internal sealed class DelegateCommandPaletteProvider(Func<CommandPaletteRequest, CancellationToken, ValueTask<IReadOnlyList<CommandPaletteEntry>>> search) : ICommandPaletteProvider
{
    public ValueTask<IReadOnlyList<CommandPaletteEntry>> GetEntriesAsync(CommandPaletteRequest request, CancellationToken cancellationToken) =>
        search(request, cancellationToken);
}

internal sealed class StaticCommandPaletteProvider(IReadOnlyList<CommandPaletteEntry> entries) : ICommandPaletteProvider
{
    public ValueTask<IReadOnlyList<CommandPaletteEntry>> GetEntriesAsync(CommandPaletteRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(entries);
}
