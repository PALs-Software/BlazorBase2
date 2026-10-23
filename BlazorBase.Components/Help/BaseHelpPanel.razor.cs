using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using BlazorBase.Components.Html;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;

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

    /// <summary>The topic that <see cref="Anchor"/> belongs to; null when it belongs to whatever topic the panel shows.</summary>
    [Parameter]
    public string? AnchorTopic { get; set; }

    /// <summary>
    /// The id of a heading or field entry to scroll to and highlight, see <see cref="AnchorRequest"/>. The panel shows
    /// <see cref="AnchorTopic"/> first when it differs from the topic that is shown.
    /// </summary>
    [Parameter]
    public string? Anchor { get; set; }

    /// <summary>
    /// Changes every time the host wants <see cref="Anchor"/> shown (also when the anchor is the same as before): a
    /// change makes the panel scroll to the anchor once the topic is shown.
    /// </summary>
    [Parameter]
    public int AnchorRequest { get; set; }

    #region Members

    private IJSObjectReference? Module;
    private IJSObjectReference? StructureModule;
    private ElementReference PanelElement;
    private ElementReference BodyElement;
    private ElementReference TocElement;
    private CancellationTokenSource? LoadSource;
    private HelpDocument? Document;
    private HelpDocumentStructure Structure = HelpDocumentStructure.Empty;
    private IReadOnlyList<HelpTocEntry> TocEntries = [];
    private TrustedMarkdownOptions? RenderOptions;
    private string? DocumentationUrl;
    private DotNetObjectReference<BaseHelpPanel>? SelfReference;
    private readonly Stack<string> History = new();
    private string? ShownTopic;
    private string? PageTopic;
    private string? LoadedPath;
    private string? PendingAnchor;
    private bool IsLoading;
    private bool IsFocusPending;
    private bool IsLinkBindingPending;
    private bool IsStructureBindingPending;
    private bool IsPanelFocusPending;
    private bool IsTocOpen;
    private bool WasOpen;
    private bool WasDocked;
    private int LoadCount;
    private int HandledAnchorRequest;

    #endregion

    private string TitleId => PanelId + "-title";

    private string TocListId => PanelId + "-toc";

    private bool ShowToc => !IsLoading && Document is not null && Structure.HasTableOfContents;

    private Icon TocChevron => IsTocOpen ? new Size16.ChevronUp() : new Size16.ChevronDown();

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
        var hasNewAnchorRequest = AnchorRequest != HandledAnchorRequest;
        HandledAnchorRequest = AnchorRequest;

        if (hasNewAnchorRequest && !string.IsNullOrWhiteSpace(Anchor))
            PendingAnchor = Anchor;

        if (!IsOpen)
        {
            CancelLoad();
            IsFocusPending = false;
            IsLinkBindingPending = false;
            IsStructureBindingPending = false;
            IsPanelFocusPending = false;
            IsTocOpen = false;
            WasOpen = false;
            History.Clear();
            ShownTopic = null;
            PageTopic = null;
            LoadedPath = null;

            if (!hasNewAnchorRequest)
                PendingAnchor = null;

            return;
        }

        if (WasOpen)
        {
            if (WasDocked != IsDocked)
            {
                IsFocusPending = true;
                IsLinkBindingPending = true;
                IsStructureBindingPending = true;
            }

            WasDocked = IsDocked;

            if (hasNewAnchorRequest && !string.IsNullOrWhiteSpace(Anchor))
                await ShowTopicAsync(AnchorTopic ?? ShownTopic, Anchor);

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

        if (IsStructureBindingPending && !IsLoading && Document is not null)
        {
            IsStructureBindingPending = false;
            StructureModule ??= await ImportStructureModuleAsync();
            await StructureModule.InvokeVoidAsync("bindHelpToc", BodyElement, ShowToc ? TocElement : (ElementReference?)null);
        }

        if (PendingAnchor is not null && !IsLoading && Document is not null)
        {
            var anchor = PendingAnchor;
            PendingAnchor = null;
            StructureModule ??= await ImportStructureModuleAsync();
            await StructureModule.InvokeVoidAsync("scrollToHelpAnchor", BodyElement, anchor);
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
        if (!IsOpen || String.Equals(topic, ShownTopic, StringComparison.OrdinalIgnoreCase))
            return;

        await ShowTopicAsync(topic, anchor: null);
    });

    /// <summary>
    /// Shows <paramref name="topic"/> like <see cref="OpenTopicAsync"/> and scrolls to the heading or field entry
    /// <paramref name="anchor"/> in it, highlighting it briefly; when the topic is already shown it only scrolls.
    /// </summary>
    /// <param name="topic">The topic, e.g. <c>general/blog</c>.</param>
    /// <param name="anchor">The id of a heading or field entry of the topic; null or empty scrolls nowhere.</param>
    [JSInvokable("OpenTopicAtAnchor")]
    public Task OpenTopicAtAnchorAsync(string topic, string? anchor) => InvokeAsync(() => IsOpen ? ShowTopicAsync(topic, anchor) : Task.CompletedTask);

    private async Task ShowTopicAsync(string? topic, string? anchor)
    {
        if (!string.IsNullOrWhiteSpace(anchor))
            PendingAnchor = anchor;

        if (topic is null || !HelpPaths.IsSafe(topic))
        {
            StateHasChanged();
            return;
        }

        if (String.Equals(topic, ShownTopic, StringComparison.OrdinalIgnoreCase))
        {
            StateHasChanged();
            return;
        }

        if (ShownTopic is not null)
            History.Push(ShownTopic);

        IsPanelFocusPending = true;
        await LoadAsync(topic, allowFallback: false);
    }

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

    private async Task<IJSObjectReference> ImportStructureModuleAsync() =>
        await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.Components/js/helpPanel.js");

    private void ToggleToc() => IsTocOpen = !IsTocOpen;

    private void SelectTocEntry(string id)
    {
        IsTocOpen = false;
        PendingAnchor = id;
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key != "Escape")
            return;

        if (IsTocOpen)
        {
            IsTocOpen = false;
            return;
        }

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
        IsTocOpen = false;
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
                renderOptions = HelpMarkdownProcessor.CreateRenderOptions(context, document);
            }

            if (source.IsCancellationRequested)
                return;

            Document = document;
            RenderOptions = renderOptions;
            Structure = document is null ? HelpDocumentStructure.Empty : HelpStructureReader.Read(document);
            TocEntries = Structure.TableOfContents;
            DocumentationUrl = content is null ? null : HelpMarkdownProcessor.GetDocumentationUrl(content.Topic, Settings.DocumentationBaseUrl, content.Culture);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IsLoading = false;
        IsStructureBindingPending = Document is not null;

        if (Document is null)
            PendingAnchor = null;

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

        if (StructureModule is not null)
        {
            try
            {
                await StructureModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }

            StructureModule = null;
        }

        GC.SuppressFinalize(this);
    }
}
