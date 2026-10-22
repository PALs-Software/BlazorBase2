using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using BlazorBase.Components.Html;

namespace BlazorBase.Components.Help;

/// <summary>
/// The contextual help of the application shell: a panel at the end edge that shows the help topic of the
/// current page — the Markdown of its <see cref="HelpTopicAttribute"/>, or the fallback topic — and follows
/// the user from page to page while it stays open.
/// </summary>
/// <remarks>
/// Where <see cref="IsDocked"/> is set the panel takes its own column next to the page, so the user can read and
/// work at the same time; otherwise it is a modal sheet over a dimmed page that holds the focus until it closes.
/// In both cases the focus moves into the panel when it opens, and Escape or the close button asks the host to
/// close it. <see cref="BaseLayout"/> renders one behind its help button (<see cref="BaseLayout.ShowHelp"/>); the
/// services come from <see cref="HelpServiceCollectionExtensions.AddBlazorBaseHelp"/>.
/// <para>
/// A link in the help Markdown to another topic that has no page (a category topic such as
/// <c>concepts/budgets-and-boosts</c>) opens inside the panel: the topic shown before is kept on a history stack and
/// a back button in the header returns to it. The stack is cleared when the panel closes and when the user
/// navigates to another page; links to topics with a page navigate to that page instead.
/// </para>
/// </remarks>
public partial class BaseHelpPanel(
    NavigationManager navigationManager,
    IHelpContentSource contentSource,
    IHelpTopicResolver topicResolver,
    IOptions<HelpOptions> options,
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseHelpPanel> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IHelpContentSource ContentSource = contentSource;
    private readonly IHelpTopicResolver TopicResolver = topicResolver;
    private readonly IOptions<HelpOptions> Options = options;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseHelpPanel> Localizer = localizer;

    #endregion

    /// <summary>Whether the panel is shown.</summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>True on a viewport wide enough to show the panel next to the page; false makes it a modal sheet.</summary>
    [Parameter]
    public bool IsDocked { get; set; }

    /// <summary>Raised when the panel asks to be closed (close button, Escape, click on the dimmed page).</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Id of the panel element, for the button that opens it to point <c>aria-controls</c> at.</summary>
    [Parameter]
    public string PanelId { get; set; } = "base-help";

    #region Members

    private IJSObjectReference? Module;
    private ElementReference PanelElement;
    private CancellationTokenSource? LoadSource;
    private HelpDocument? Document;
    private TrustedMarkdownOptions? RenderOptions;
    private string? DocumentationUrl;
    private DotNetObjectReference<BaseHelpPanel>? SelfReference;
    private readonly Stack<string> History = new();
    private string? ShownTopic;
    private string? PageTopic;
    private string? LoadedPath;
    private bool IsLoading;
    private bool IsFocusPending;
    private bool IsLinkBindingPending;
    private bool IsPanelFocusPending;
    private bool WasOpen;
    private bool WasDocked;
    private int LoadCount;

    #endregion

    private string TitleId => PanelId + "-title";

    private bool CanGoBack => History.Count > 0;

    private HelpOptions Settings => Options.Value;

    private string RequestedTopic => TopicResolver.ResolveTopic(CurrentPath) ?? Settings.FallbackTopic;

    private string CurrentPath
    {
        get
        {
            var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            var end = path.IndexOfAny(['?', '#']);
            return end < 0 ? path : path[..end];
        }
    }

    protected override void OnInitialized() => NavigationManager.LocationChanged += OnLocationChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen)
        {
            CancelLoad();
            IsFocusPending = false;
            IsLinkBindingPending = false;
            IsPanelFocusPending = false;
            WasOpen = false;
            History.Clear();
            ShownTopic = null;
            PageTopic = null;
            LoadedPath = null;
            return;
        }

        if (WasOpen)
        {
            if (WasDocked != IsDocked)
            {
                IsFocusPending = true;
                IsLinkBindingPending = true;
            }

            WasDocked = IsDocked;
            return;
        }

        WasOpen = true;
        WasDocked = IsDocked;
        IsFocusPending = true;
        IsLinkBindingPending = true;
        LoadedPath = CurrentPath;
        await LoadPageTopicAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!IsOpen)
            return;

        if (IsLinkBindingPending)
        {
            IsLinkBindingPending = false;
            Module ??= await ImportModuleAsync();
            SelfReference ??= DotNetObjectReference.Create(this);
            await Module.InvokeVoidAsync("bindHelpLinks", PanelElement, SelfReference);
        }

        if (IsPanelFocusPending && !IsLoading)
        {
            IsPanelFocusPending = false;
            await PanelElement.FocusAsync();
        }

        if (!IsFocusPending)
            return;

        IsFocusPending = false;

        if (IsDocked)
        {
            await PanelElement.FocusAsync();
            return;
        }

        Module ??= await ImportModuleAsync();
        await Module.InvokeVoidAsync("trapFocus", PanelElement);
    }

    /// <summary>
    /// Called by the panel's script when the user activates a link to a topic that has no page; shows that topic and
    /// keeps the one shown before for the back button. Does nothing for a topic that is not a plain relative path.
    /// </summary>
    /// <param name="topic">The topic, e.g. <c>concepts/budgets-and-boosts</c>.</param>
    [JSInvokable]
    public Task OpenTopicAsync(string topic) => InvokeAsync(async () =>
    {
        if (!IsOpen || !HelpPaths.IsSafe(topic) || String.Equals(topic, ShownTopic, StringComparison.OrdinalIgnoreCase))
            return;

        if (ShownTopic is not null)
            History.Push(ShownTopic);

        IsPanelFocusPending = true;
        await LoadAsync(topic, allowFallback: false);
    });

    private async Task BackAsync()
    {
        if (!CanGoBack)
            return;

        var previous = History.Pop();
        IsPanelFocusPending = true;
        await LoadAsync(previous, allowFallback: !CanGoBack);
    }

    private Task CloseAsync() => OnClose.InvokeAsync();

    private async Task<IJSObjectReference> ImportModuleAsync() =>
        await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.Components/js/layout.js");

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            await CloseAsync();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (!IsOpen)
            return;

        var path = CurrentPath;

        if (String.Equals(path, LoadedPath, StringComparison.OrdinalIgnoreCase))
            return;

        LoadedPath = path;

        if (!CanGoBack && String.Equals(PageTopic, RequestedTopic, StringComparison.Ordinal))
            return;

        _ = InvokeAsync(LoadPageTopicAsync);
    }

    private Task LoadPageTopicAsync()
    {
        History.Clear();
        PageTopic = RequestedTopic;

        return LoadAsync(PageTopic, allowFallback: true);
    }

    private async Task LoadAsync(string topic, bool allowFallback)
    {
        CancelLoad();
        var source = LoadSource = new CancellationTokenSource();
        ShownTopic = topic;
        LoadCount++;
        IsLoading = true;
        StateHasChanged();

        try
        {
            var content = await ContentSource.GetTopicAsync(topic, CultureInfo.CurrentUICulture.Name, source.Token);

            if (content is not null && !allowFallback && !String.Equals(content.Topic, topic, StringComparison.OrdinalIgnoreCase))
                content = null;

            HelpDocument? document = null;
            TrustedMarkdownOptions? renderOptions = null;

            if (content is not null)
            {
                var context = CreateContext(content);
                document = await HelpMarkdownProcessor.ProcessAsync(content.Markdown, context, source.Token);
                renderOptions = HelpMarkdownProcessor.CreateRenderOptions(context);
            }

            if (source.IsCancellationRequested)
                return;

            Document = document;
            RenderOptions = renderOptions;
            DocumentationUrl = content is null ? null : HelpMarkdownProcessor.GetDocumentationUrl(content.Topic, Settings.DocumentationBaseUrl, content.Culture);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IsLoading = false;
        StateHasChanged();
    }

    private HelpProcessingContext CreateContext(HelpSourceDocument content) => new()
    {
        Topic = content.Topic,
        ContentRootUrl = Settings.ContentBasePath.Trim('/') + "/" + content.Culture + "/",
        LoadFileAsync = (path, token) => ContentSource.GetFileAsync(content.Culture, path, token),
        ResolveRoute = TopicResolver.GetRoute,
        DocumentationBaseUrl = Settings.DocumentationBaseUrl,
        Culture = content.Culture,
        AlertTitles = new Dictionary<string, string>
        {
            ["NOTE"] = Localizer["AlertNote"],
            ["TIP"] = Localizer["AlertTip"],
            ["IMPORTANT"] = Localizer["AlertImportant"],
            ["WARNING"] = Localizer["AlertWarning"],
            ["CAUTION"] = Localizer["AlertCaution"],
        },
    };

    private void CancelLoad()
    {
        LoadSource?.Cancel();
        LoadSource?.Dispose();
        LoadSource = null;
    }

    public async ValueTask DisposeAsync()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        CancelLoad();
        SelfReference?.Dispose();
        SelfReference = null;

        if (Module is not null)
        {
            try
            {
                await Module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }

            Module = null;
        }

        GC.SuppressFinalize(this);
    }
}
