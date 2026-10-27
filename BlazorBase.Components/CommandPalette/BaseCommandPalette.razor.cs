using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using Microsoft.JSInterop;

namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// The command palette: a centered, searchable list of pages, apps and actions that the user opens with Ctrl+K (or a
/// plain "/"), narrows by typing, moves through with the arrow keys and runs with Enter.
/// </summary>
/// <remarks>
/// <c>BaseLayout</c> renders it once when <c>ShowCommandPalette</c> is set; open it from code through
/// <see cref="ICommandPaletteService"/>. The entries come from the <see cref="ICommandPaletteProvider"/> services of the
/// application plus <see cref="Providers"/>; the palette filters them by <see cref="CommandPaletteEntry.RequiredRole"/>,
/// ranks them and caps them (see <see cref="CommandPaletteOptions"/>). It is built from plain elements: the focus
/// returns to the previously focused element on close, Tab stays inside, and on a phone (768 px and below) it
/// fills the screen.
/// </remarks>
public partial class BaseCommandPalette(
    ICommandPaletteService service,
    IOptions<CommandPaletteOptions> options,
    IEnumerable<ICommandPaletteProvider> registeredProviders,
    NavigationManager navigationManager,
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseCommandPalette> localizer,
    IServiceProvider serviceProvider) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly ICommandPaletteService Service = service;
    private readonly CommandPaletteOptions Options = options.Value;
    private readonly IEnumerable<ICommandPaletteProvider> RegisteredProviders = registeredProviders;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseCommandPalette> Localizer = localizer;
    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    /// <summary>Providers in addition to the registered ones, for example the built-in ones <c>BaseLayout</c> creates from its parameters.</summary>
    [Parameter]
    public IReadOnlyList<ICommandPaletteProvider>? Providers { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    private sealed record Row(int Index, CommandPaletteEntry Entry);

    private sealed record GroupView(string Name, string Id, IReadOnlyList<Row> Rows);

    private static readonly string[] AllowedSchemes = ["http", "https", "mailto", "tel"];

    private static int Counter;

    private readonly string PaletteId = $"base-command-palette-{Interlocked.Increment(ref Counter)}";

    private ILogger Logger = NullLogger.Instance;
    private CommandPaletteSearcher Searcher = null!;
    private IJSObjectReference? Module;
    private DotNetObjectReference<BaseCommandPalette>? SelfReference;
    private CancellationTokenSource? SearchCancellation;
    private Task SearchTask = Task.CompletedTask;
    private ElementReference Panel;
    private ElementReference Input;
    private ElementReference Body;
    private IReadOnlyList<GroupView> Groups = [];
    private IReadOnlyList<Row> Rows = [];
    private IReadOnlyList<string> QueryTokens = [];
    private IReadOnlyList<string> RecentIds = [];
    private string Query = string.Empty;
    private string StorageKey = string.Empty;
    private int ActiveIndex;
    private int SearchVersion;
    private int CompletedVersion;
    private long LastKeyboardMoveTicks;
    private bool IsShown;
    private bool IsActivated;
    private bool IsLoading;
    private bool HasSearched;
    private bool ScrollPending;
    private bool IsDisposed;

    private string ListId => $"{PaletteId}-list";

    private string? ActiveDescendantId => Rows.Count == 0 ? null : OptionId(ActiveIndex);

    private bool ShowsEmptyState => HasSearched && !IsLoading && Rows.Count == 0;

    private string StatusText => IsLoading
        ? Localizer["Loading"]
        : !HasSearched ? string.Empty
        : Rows.Count == 0 ? Localizer["NoResults"]
        : Rows.Count == 1 ? Localizer["ResultCountOne"]
        : Localizer["ResultCount", Rows.Count];

    private IReadOnlyList<ICommandPaletteProvider> AllProviders => [.. RegisteredProviders, .. Providers ?? []];

    private string OptionId(int index) => $"{PaletteId}-option-{index}";

    protected override void OnInitialized()
    {
        Logger = (ILogger?)ServiceProvider.GetService<ILogger<BaseCommandPalette>>() ?? NullLogger.Instance;
        Searcher = new CommandPaletteSearcher(Options, Logger, Localizer["GroupRecent"]);
        Service.Changed += OnServiceChanged;
        IsShown = Service.IsOpen;
        Query = Service.InitialQuery ?? string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsDisposed)
            return;

        if (firstRender)
        {
            var module = await LoadModuleAsync();
            SelfReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("registerShortcut", SelfReference, Options.SlashOpensPalette);

            if (IsShown)
                await StartSessionAsync();
        }

        if (IsShown && !IsActivated)
        {
            IsActivated = true;
            await (await LoadModuleAsync()).InvokeVoidAsync("activate", Panel, Input, SelfReference);
        }
        else if (!IsShown && IsActivated)
        {
            IsActivated = false;
            await (await LoadModuleAsync()).InvokeVoidAsync("deactivate");
        }

        if (ScrollPending && IsShown)
        {
            ScrollPending = false;
            await (await LoadModuleAsync()).InvokeVoidAsync("scrollToActive", Body);
        }
    }

    /// <summary>Called from the browser when Ctrl or Cmd plus K (<paramref name="toggle"/>) or a plain slash was pressed.</summary>
    [JSInvokable]
    public Task OnShortcut(bool toggle) => InvokeAsync(async () =>
    {
        if (toggle && Service.IsOpen)
            await Service.CloseAsync();
        else
            await Service.OpenAsync();
    });

    /// <summary>Called from the browser when Escape was pressed while the palette is shown, wherever the focus is.</summary>
    [JSInvokable]
    public Task OnEscape() => InvokeAsync(Service.CloseAsync);

    private void OnServiceChanged() => _ = InvokeAsync(HandleServiceChangedAsync);

    private async Task HandleServiceChangedAsync()
    {
        if (IsDisposed)
            return;

        if (!Service.IsOpen)
        {
            if (!IsShown)
                return;

            IsShown = false;
            CancelSearch();
            StateHasChanged();
            return;
        }

        var wasShown = IsShown;
        if (wasShown && Service.InitialQuery is null)
            return;

        IsShown = true;
        Query = Service.InitialQuery ?? string.Empty;

        if (!wasShown)
        {
            Groups = [];
            Rows = [];
            HasSearched = false;
        }

        StateHasChanged();
        await StartSessionAsync();
    }

    private async Task StartSessionAsync()
    {
        await LoadRecentAsync();
        SearchTask = RunSearchAsync(immediate: true);
    }

    private Task CloseAsync() => Service.CloseAsync();

    private void OnInput(ChangeEventArgs args)
    {
        Query = args.Value?.ToString() ?? string.Empty;
        SearchTask = RunSearchAsync(immediate: false);
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Escape":
                await Service.CloseAsync();
                break;
            case "ArrowDown":
                MoveActive(1);
                break;
            case "ArrowUp":
                MoveActive(-1);
                break;
            case "Home" when MovesListWithHomeEnd(args):
                SetActive(0);
                break;
            case "End" when MovesListWithHomeEnd(args):
                SetActive(Rows.Count - 1);
                break;
            case "Enter":
                await ActivateActiveAsync();
                break;
        }
    }

    private bool MovesListWithHomeEnd(KeyboardEventArgs args) => Query.Length == 0 || args.CtrlKey;

    private void MoveActive(int delta)
    {
        if (Rows.Count == 0)
            return;

        SetActive((ActiveIndex + delta + Rows.Count) % Rows.Count);
    }

    private void SetActive(int index)
    {
        if (Rows.Count == 0)
            return;

        ActiveIndex = Math.Clamp(index, 0, Rows.Count - 1);
        ScrollPending = true;
        LastKeyboardMoveTicks = Environment.TickCount64;
    }

    private void OnRowHover(int index)
    {
        if (index == ActiveIndex || Environment.TickCount64 - LastKeyboardMoveTicks < 250)
            return;

        ActiveIndex = index;
    }

    private async Task ActivateActiveAsync()
    {
        await SearchTask;

        if (ActiveIndex >= 0 && ActiveIndex < Rows.Count)
            await ActivateAsync(Rows[ActiveIndex].Entry);
    }

    private async Task ActivateAsync(CommandPaletteEntry entry)
    {
        await RememberAsync(entry);
        await Service.CloseAsync();
        await RunAsync(entry);
    }

    private async Task RunAsync(CommandPaletteEntry entry)
    {
        try
        {
            if (entry.Execute is { } execute)
                await execute();
            else
                await OpenTargetAsync(entry);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "The command palette entry {EntryId} failed.", entry.Id);
            ServiceProvider.GetService<IToastService>()?.ShowError(Localizer["ActionFailed"]);
        }
    }

    private async Task OpenTargetAsync(CommandPaletteEntry entry)
    {
        var href = entry.Href!;
        var isAbsolute = TryParseAbsolute(href, out var absolute);

        if (isAbsolute && !AllowedSchemes.Contains(absolute!.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            Logger.LogWarning("The command palette entry {EntryId} points to a disallowed address scheme and was not opened.", entry.Id);
            return;
        }

        var isExternal = isAbsolute && !href.StartsWith(NavigationManager.BaseUri, StringComparison.OrdinalIgnoreCase);
        if (!isExternal && !entry.OpenInNewTab)
        {
            NavigationManager.NavigateTo(href);
            return;
        }

        var target = isAbsolute ? href : NavigationManager.ToAbsoluteUri(href).ToString();
        await (await LoadModuleAsync()).InvokeVoidAsync("openExternal", target);
    }

    private async Task RunSearchAsync(bool immediate)
    {
        CancelSearch();
        var cancellation = SearchCancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var version = ++SearchVersion;

        try
        {
            if (!immediate && Options.SearchDebounce > TimeSpan.Zero)
                await Task.Delay(Options.SearchDebounce, token);

            _ = ShowLoadingLaterAsync(version, token);

            var user = await ResolveUserAsync();
            var result = await Searcher.SearchAsync(AllProviders, Query, user, CultureInfo.CurrentUICulture, RecentIds, token);
            if (token.IsCancellationRequested)
                return;

            CompletedVersion = version;
            Apply(result);
            StateHasChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "The command palette search failed.");
            if (version == SearchVersion)
            {
                CompletedVersion = version;
                Apply(CommandPaletteResult.Empty);
                StateHasChanged();
            }
        }
    }

    private async Task ShowLoadingLaterAsync(int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(Options.LoadingIndicatorDelay, token);
            await InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || version != SearchVersion || version <= CompletedVersion)
                    return;

                IsLoading = true;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Apply(CommandPaletteResult result)
    {
        var rows = new List<Row>();
        var groups = new List<GroupView>();
        foreach (var group in result.Groups)
        {
            var firstIndex = rows.Count;
            var groupRows = group.Entries.Select((entry, offset) => new Row(firstIndex + offset, entry)).ToList();
            rows.AddRange(groupRows);
            groups.Add(new GroupView(group.Name, $"{PaletteId}-group-{groups.Count}", groupRows));
        }

        QueryTokens = CommandPaletteText.Tokenize(Query);
        Groups = groups;
        Rows = rows;
        ActiveIndex = 0;
        ScrollPending = true;
        HasSearched = true;
        IsLoading = false;
    }

    private void CancelSearch()
    {
        SearchCancellation?.Cancel();
        IsLoading = false;
    }

    private async Task<ClaimsPrincipal> ResolveUserAsync()
    {
        if (AuthenticationState is null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        return (await AuthenticationState).User;
    }

    private async Task LoadRecentAsync()
    {
        var user = await ResolveUserAsync();
        StorageKey = $"blazorbase.commandpalette.recent:{user.Identity?.Name ?? "anonymous"}";

        try
        {
            var module = await LoadModuleAsync();
            RecentIds = await module.InvokeAsync<string[]?>("readRecent", StorageKey) ?? [];
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
            RecentIds = [];
        }
    }

    private async Task RememberAsync(CommandPaletteEntry entry)
    {
        RecentIds = CommandPaletteRecents.Push(RecentIds, entry.Id, Options.MaxRecent);

        try
        {
            await (await LoadModuleAsync()).InvokeVoidAsync("writeRecent", StorageKey, RecentIds);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    private async Task<IJSObjectReference> LoadModuleAsync() =>
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/commandPalette.js");

    private RenderFragment Highlight(string text) => builder =>
    {
        var sequence = 0;
        foreach (var segment in CommandPaletteText.Highlight(text, QueryTokens))
        {
            if (!segment.IsMatch)
            {
                builder.AddContent(sequence++, segment.Text);
                continue;
            }

            builder.OpenElement(sequence++, "mark");
            builder.AddContent(sequence++, segment.Text);
            builder.CloseElement();
        }
    };

    private static Icon DefaultIcon(CommandPaletteEntry entry) =>
        entry.Execute is not null ? new Size20.Flash()
        : IsExternalAddress(entry) ? new Size20.Open()
        : new Size20.Document();

    private string HintFor(CommandPaletteEntry entry) =>
        entry.Hint
        ?? (entry.Execute is not null ? Localizer["HintAction"] : IsExternalAddress(entry) ? Localizer["HintLink"] : Localizer["HintPage"]);

    private static bool IsExternalAddress(CommandPaletteEntry entry) =>
        entry.Href is not null && TryParseAbsolute(entry.Href, out _);

    /// <summary>A root relative address such as "/Blog" is an absolute file address on Unix based runtimes (WebAssembly); it is an address inside the application. Protocol relative addresses ("//host") are not.</summary>
    private static bool TryParseAbsolute(string href, out Uri? absolute)
    {
        absolute = null;
        var isInternal = href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal);
        return !isInternal && Uri.TryCreate(href, UriKind.Absolute, out absolute);
    }

    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        Service.Changed -= OnServiceChanged;
        SearchCancellation?.Cancel();

        if (Module is not null)
        {
            try
            {
                await Module.InvokeVoidAsync("unregisterShortcut");
                await Module.InvokeVoidAsync("deactivate");
                await Module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or OperationCanceledException or InvalidOperationException)
            {
            }
        }

        SelfReference?.Dispose();
    }
}
