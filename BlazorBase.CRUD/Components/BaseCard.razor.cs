using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Display;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Security;
using BlazorBase.Components.Services;
using BlazorBase.Components.Assistant;
using BlazorBase.CRUD.Validation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

public partial class BaseCard<TModel> : ComponentBase, IFieldCollector<TModel>, IListPartCollector<TModel>, IActionGroupCollector<TModel>, IDisplayKeyCollector<TModel>, IAssistantContextProvider, IAsyncDisposable
    where TModel : class, new()
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    #endregion

    private ElementReference CardRootElement { get; set; }
    private CardErrorBoundary? ErrorBoundary { get; set; }
    private IJSObjectReference? DismissModule { get; set; }
    private DotNetObjectReference<BaseCard<TModel>>? SelfReference { get; set; }
    private string? DismissToken { get; set; }

    [Parameter]
    public TModel Model { get; set; } = new();

    [Parameter]
    public IBaseDataProvider<TModel>? DataProvider { get; set; }

    /// <summary>
    /// Whether the model is being created rather than edited. Set it when the caller knows —
    /// <see cref="BaseDialog{TModel}"/> passes what <see cref="BaseList{TModel}"/> already decided.
    /// </summary>
    /// <remarks>
    /// Left unset, the key is inspected instead, which can only ever be a guess: a model that
    /// assigns its key before saving looks like an existing row. Getting it wrong is not cosmetic —
    /// it decides the dialog caption, whether insert or modify rights are checked, and whether
    /// saving creates or updates.
    /// </remarks>
    [Parameter]
    public bool? IsNew { get; set; }

    /// <summary>
    /// What the hosting dialog knows, for the case where this card sits inside a custom
    /// <c>CardType</c> that does not forward <see cref="IsNew"/> itself.
    /// </summary>
    [CascadingParameter(Name = CardCascadeNames.IsNew)]
    private bool? DialogIsNew { get; set; }

    [Parameter]
    public BaseCardConfiguration<TModel>? Configuration { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public int MaxColumns { get; set; } = 3;

    [Parameter]
    public bool ExpandAllGroups { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public string? SaveButtonText { get; set; }

    [Parameter]
    public string? CancelButtonText { get; set; }

    [Parameter]
    public bool DeferSave { get; set; }

    /// <summary>
    /// Whether the header shows the close ("X") button. Left unset, the button is shown when something can act on a
    /// close request — a dialog hosts the card or <see cref="OnCancelled"/> has a consumer — and hidden on a card
    /// shown standalone on a page, where closing has no meaning.
    /// </summary>
    [Parameter]
    public bool? ShowCloseButton { get; set; }

    /// <summary>
    /// The dialog hosting the card, if any; its presence is what tells a dialog card from one shown on a page.
    /// </summary>
    [CascadingParameter]
    private IDialogInstance? HostingDialog { get; set; }

    private bool IsInline => HostingDialog is null;

    /// <summary>
    /// The hook of the list that opened this card in a dialog, used to confirm unsaved changes before the browser
    /// navigates away from the card (back, forward, a link).
    /// </summary>
    [CascadingParameter]
    private CardCloseGuard? CloseGuard { get; set; }

    private bool HasCloseConsumer => HostingDialog is not null || OnCancelled.HasDelegate;

    private bool IsCloseButtonVisible => ShowCloseButton ?? HasCloseConsumer;

    /// <summary>
    /// Separator placed between multiple display-key values in the header title.
    /// Overridden by <see cref="BaseCardConfiguration{TModel}.DisplayKeySeparator"/>;
    /// null falls back to <see cref="DisplayKeyResolver.DefaultSeparator"/>.
    /// </summary>
    [Parameter]
    public string? DisplayKeySeparator { get; set; }

    [Parameter]
    public EventCallback<Dictionary<string, object?>> OnDeferredSave { get; set; }

    private IStringLocalizer FrameworkLocalizer =>
        LocalizerResolver.ResolveFramework(ServiceProvider);

    private IStringLocalizer ResolvedLocalizer =>
        LocalizerResolver.ResolveProperty(ServiceProvider, Localizer, typeof(TModel));

    private string ResolveListPartTitle(BaseListPartConfiguration<TModel> listPart)
    {
        var result = ResolvedLocalizer[listPart.PropertyName];
        return result.ResourceNotFound ? listPart.PropertyName : result.Value;
    }

    private string? ResolveListPartTooltip(BaseListPartConfiguration<TModel> listPart) =>
        LocalizerResolver.ResolveOptional(ResolvedLocalizer, $"{listPart.PropertyName}_Tooltip");

    private CardFieldErrors FieldErrors { get; } = new();

    private bool FocusFirstErrorPending { get; set; }

    private string ResolveFieldLabel(string propertyName)
    {
        var configuredLabel = ResolvedFields.FirstOrDefault(field => field.PropertyName == propertyName)?.Label;
        if (configuredLabel is not null)
            return configuredLabel;

        var result = ResolvedLocalizer[propertyName];
        return result.ResourceNotFound ? propertyName : result.Value;
    }

    /// <summary>
    /// Whether the field takes a whole grid row: explicitly requested, a multi-line editor, or a column span that
    /// covers every column the card can have.
    /// </summary>
    private bool IsFullRowField(PropertyFieldConfig<TModel> field) =>
        field.FullWidth || field.Lines > 1 || field.ColSpan >= MaxColumns;

    private Task OnInvalidSubmitAsync()
    {
        FocusFirstErrorPending = true;
        return Task.CompletedTask;
    }

    #region Events

    [Parameter]
    public EventCallback<PropertyChangedEventArgs<TModel>> OnBeforePropertyChanged { get; set; }

    [Parameter]
    public EventCallback<PropertyChangedEventArgs<TModel>> OnAfterPropertyChanged { get; set; }

    [Parameter]
    public EventCallback<TModel> OnBeforeSave { get; set; }

    [Parameter]
    public EventCallback<TModel> OnAfterSave { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    #endregion

    private static readonly HashSet<string> NavigationPropertyNames =
        new(NavigationPropertyResolver.GetNavigationProperties(typeof(TModel))
            .Select(n => n.PropertyName), StringComparer.OrdinalIgnoreCase);

    private List<PropertyFieldConfig<TModel>> CollectedFields { get; } = [];
    private List<BaseListPartConfiguration<TModel>> CollectedListParts { get; } = [];
    private List<CrudActionGroup<TModel>> CollectedActionGroups { get; } = [];
    private List<DisplayKeyFieldConfig<TModel>> CollectedDisplayKeys { get; } = [];
    private List<ICardSaveParticipant> SaveParticipants { get; } = [];
    private Dictionary<string, object?> OriginalValues { get; set; } = [];
    private Dictionary<string, IListPartFlushable> RegisteredListParts { get; } = [];
    private string? ConcurrencyStamp { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsConfirmDialogOpen { get; set; }
    private bool InitialSnapshotTaken { get; set; }
    private TModel? SnapshotParameterModel { get; set; }
    private int LoadedNavigationFields { get; set; }

    private IBaseDataProvider<TModel> ResolvedDataProvider =>
        DataProvider ?? ServiceProvider.GetService<IBaseDataProvider<TModel>>()
        ?? throw new InvalidOperationException($"No DataProvider available for {typeof(TModel).Name}. Register via DI or pass as parameter.");

    private List<PropertyFieldConfig<TModel>> ResolvedFields =>
        Configuration?.Fields ?? CollectedFields;

    private List<BaseListPartConfiguration<TModel>> ResolvedListParts =>
        Configuration?.ListParts ?? CollectedListParts;

    private List<CrudActionGroup<TModel>> ResolvedActionGroups =>
        Configuration?.ActionGroups ?? CollectedActionGroups;

    private bool HasCardActions =>
        ResolvedActionGroups.Any(g => g.Contexts.HasFlag(CrudActionContext.Card));

    private Dictionary<string, List<PropertyFieldConfig<TModel>>> GroupedFields
    {
        get
        {
            var fieldsWithGroups = VisibleFieldsForUser.Where(f => !string.IsNullOrEmpty(f.Group)).ToList();

            if (fieldsWithGroups.Count == 0)
                return new Dictionary<string, List<PropertyFieldConfig<TModel>>>();

            return fieldsWithGroups
                .GroupBy(f => f.Group!)
                .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Order).ToList());
        }
    }

    #region Access Rights

    private ClaimsPrincipal? CurrentUser { get; set; }

    private CrudRights ClassRightsForCurrentUser =>
        CurrentUser is null ? CrudRights.All : CrudAccessResolver.EvaluateClass(typeof(TModel), CurrentUser);

    private bool IsFieldVisibleForUser(PropertyFieldConfig<TModel> field)
    {
        if (CurrentUser is null)
            return true;

        var effective = CrudAccessResolver.EvaluateProperty(typeof(TModel), field.PropertyName, CurrentUser);
        effective = CrudAccessResolver.ApplyViewRights(effective, field.AccessRules, CurrentUser);
        return (effective & CrudRights.Read) == CrudRights.Read;
    }

    private bool IsFieldEditableForUser(PropertyFieldConfig<TModel> field)
    {
        if (ReadOnly || !field.Editable)
            return false;

        if (CurrentUser is null)
            return true;

        var effective = CrudAccessResolver.EvaluateProperty(typeof(TModel), field.PropertyName, CurrentUser);
        effective = CrudAccessResolver.ApplyViewRights(effective, field.AccessRules, CurrentUser);
        return (effective & CrudRights.Modify) == CrudRights.Modify;
    }

    private IEnumerable<PropertyFieldConfig<TModel>> VisibleFieldsForUser =>
        ResolvedFields.Where(IsFieldVisibleForUser);

    private static bool IsReferenceNavigationField(PropertyFieldConfig<TModel> field)
    {
        var navigation = NavigationPropertyResolver.GetNavigationProperty(typeof(TModel), field.PropertyName);

        if (navigation is null || navigation.IsCollection)
            return false;

        return NavigationPropertyResolver.GetForeignKeyProperty(typeof(TModel), field.PropertyName) is not null;
    }

    private int ExpectedNavigationFieldCount =>
        VisibleFieldsForUser.Count(IsReferenceNavigationField);

    /// <summary>
    /// True while any reference-navigation field is still loading its lookup options. The card shows a loading
    /// indicator and keeps its content hidden during this window so every field appears at once, instead of the
    /// asynchronously-loaded lookups popping in after the plain fields are already visible.
    /// </summary>
    private bool IsContentLoading => LoadedNavigationFields < ExpectedNavigationFieldCount;

    private bool IsNewModel => IsNew ?? DialogIsNew ?? InferIsNewFromKey();

    /// <summary>
    /// Falls back to the key when neither the caller nor a hosting dialog said whether the model is new.
    /// </summary>
    /// <remarks>
    /// An empty string counts as unset. A string key is conventionally initialised to
    /// <see cref="string.Empty"/> rather than left null, and comparing it against the CLR default
    /// (<c>null</c>) made every new model look like an existing row — which captioned the dialog
    /// "edit", checked modify instead of insert rights, and sent the save down the update path.
    /// </remarks>
    private IDisposable? AssistantRegistration { get; set; }

    void IAssistantContextProvider.Contribute(AssistantContextBuilder builder)
    {
        builder.EntityType = typeof(TModel).Name;

        if (IsNewModel)
            return;

        var key = EntityKeyResolver.Find(typeof(TModel))?.GetValue(Model);
        builder.EntityKey = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? builder.EntityKey;
    }

    private bool InferIsNewFromKey()
    {
        var idProperty = EntityKeyResolver.Find(typeof(TModel));
        var idValue = idProperty?.GetValue(Model);

        if (idValue is null)
            return true;

        if (idValue is string key)
            return key.Length == 0;

        return idValue.Equals(GetDefaultValue(idProperty!.PropertyType));
    }

    private string CardTitle
    {
        get
        {
            if (IsNewModel)
                return FrameworkLocalizer["DialogTitleAdd"].Value;

            var displayKeyText = DisplayKeyResolver.BuildDisplayString(
                Model, ResolveDisplayKeyPropertyNames(), ResolvedDisplayKeySeparator);

            if (ReadOnly)
            {
                var view = FrameworkLocalizer["DialogTitleView"].Value;

                return string.IsNullOrEmpty(displayKeyText) ? view : $"{view} – {displayKeyText}";
            }

            return string.IsNullOrEmpty(displayKeyText) ? FrameworkLocalizer["DialogTitleEdit"].Value : displayKeyText;
        }
    }

    private string ResolvedDisplayKeySeparator =>
        Configuration?.DisplayKeySeparator ?? DisplayKeySeparator ?? DisplayKeyResolver.DefaultSeparator;

    private List<string> ResolveDisplayKeyPropertyNames()
    {
        var cardLevelKeys = new List<DisplayKeyFieldConfig<TModel>>();

        if (Configuration is not null)
        {
            cardLevelKeys.AddRange(Configuration.DisplayKeys);
            cardLevelKeys.AddRange(Configuration.Fields
                .Where(field => field.IsDisplayKey)
                .Select(field => new DisplayKeyFieldConfig<TModel> { PropertyName = field.PropertyName, Order = field.DisplayKeyOrder }));
        }

        cardLevelKeys.AddRange(CollectedDisplayKeys);

        if (cardLevelKeys.Count > 0)
            return cardLevelKeys.OrderBy(key => key.Order).Select(key => key.PropertyName).Distinct().ToList();

        var attributeKeys = DisplayKeyResolver.GetAttributeDisplayKeyPropertyNames(typeof(TModel));
        if (attributeKeys.Count > 0)
            return [.. attributeKeys];

        return [DisplayKeyResolver.GetPrimaryKeyPropertyName(typeof(TModel))];
    }

    private bool CanSave
    {
        get
        {
            if (ReadOnly)
                return false;

            var required = IsNewModel ? CrudRights.Insert : CrudRights.Modify;
            return (ClassRightsForCurrentUser & required) == required;
        }
    }

    #endregion

    void IFieldCollector<TModel>.AddField(PropertyFieldConfig<TModel> field)
    {
        CollectedFields.Add(field);
    }

    void IFieldCollector<TModel>.RegisterSaveParticipant(ICardSaveParticipant participant)
    {
        if (!SaveParticipants.Contains(participant))
            SaveParticipants.Add(participant);
    }

    void IFieldCollector<TModel>.UnregisterSaveParticipant(ICardSaveParticipant participant)
    {
        SaveParticipants.Remove(participant);
    }

    void IFieldCollector<TModel>.NotifyNavigationFieldLoaded()
    {
        LoadedNavigationFields++;
        StateHasChanged();
    }

    void IListPartCollector<TModel>.AddListPart(BaseListPartConfiguration<TModel> listPart)
    {
        CollectedListParts.Add(listPart);
    }

    void IActionGroupCollector<TModel>.AddActionGroup(CrudActionGroup<TModel> group)
    {
        CollectedActionGroups.Add(group);
    }

    void IDisplayKeyCollector<TModel>.AddDisplayKey(DisplayKeyFieldConfig<TModel> displayKey)
    {
        CollectedDisplayKeys.Add(displayKey);
    }

    protected override void OnParametersSet()
    {
        if (Configuration?.Fields is not null)
        {
            foreach (var field in Configuration.Fields)
                field.ResolvePropertyName();
        }

        if (Configuration?.DisplayKeys is not null)
        {
            foreach (var displayKey in Configuration.DisplayKeys)
                displayKey.ResolvePropertyName();
        }

        if (InitialSnapshotTaken && ReferenceEquals(SnapshotParameterModel, Model))
            return;

        InitialSnapshotTaken = true;
        SnapshotParameterModel = Model;
        SnapshotOriginalValues();
    }

    protected override async Task OnInitializedAsync()
    {
        AssistantRegistration = ServiceProvider.GetService<AssistantContextService>()?.Register(this);
        CloseGuard?.Register(this, () => HasUnsavedChanges, RequestGuardedCloseAsync);

        var authStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (authStateProvider is null)
            return;

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        CurrentUser = authState.User;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (FocusFirstErrorPending)
            await FocusFirstErrorAsync();

        if (!firstRender)
            return;

        if (CollectedDisplayKeys.Count > 0 || CollectedListParts.Count > 0)
            StateHasChanged();

        DismissModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.CRUD/js/baseCardDismiss.js");

        if (DismissModule is null)
            return;

        SelfReference = DotNetObjectReference.Create(this);
        DismissToken = await DismissModule.InvokeAsync<string?>("register", CardRootElement, SelfReference);
    }

    private async Task FocusFirstErrorAsync()
    {
        FocusFirstErrorPending = false;

        if (DismissModule is null)
            return;

        try
        {
            await DismissModule.InvokeVoidAsync("focusFirstError", CardRootElement);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Invoked from the dismiss interop shim when the user requests closing the dialog via
    /// Escape or an overlay click. Routes through the same confirmation pipeline as the buttons.
    /// </summary>
    [JSInvokable]
    public async Task RequestCloseAsync()
    {
        if (IsConfirmDialogOpen)
            return;

        await TryCloseAsync();
        StateHasChanged();
    }

    private void SnapshotOriginalValues()
    {
        OriginalValues.Clear();

        foreach (var property in typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
                continue;

            if (NavigationPropertyNames.Contains(property.Name))
                continue;

            OriginalValues[property.Name] = property.GetValue(Model);
        }

        if (Model is AuditModel auditModel)
            ConcurrencyStamp = auditModel.ModifiedOn?.ToString("O");
    }

    private Dictionary<string, object?> GetChangedFields()
    {
        var changedFields = new Dictionary<string, object?>();

        foreach (var property in typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            if (NavigationPropertyNames.Contains(property.Name))
                continue;

            var currentValue = property.GetValue(Model);
            OriginalValues.TryGetValue(property.Name, out var originalValue);

            if (!Equals(currentValue, originalValue))
                changedFields[property.Name] = currentValue;
        }

        return changedFields;
    }

    private async Task OnValidSubmitAsync()
    {
        ErrorMessage = null;

        var customValidators = ServiceProvider.GetServices<IBaseValidator<TModel>>();
        foreach (var validator in customValidators)
        {
            var results = (await validator.ValidateAsync(Model)).ToList();

            if (results.Count == 0)
                continue;

            ErrorMessage = string.Join(" ", results.Select(r => r.ErrorMessage));
            StateHasChanged();
            return;
        }

        var idProperty = EntityKeyResolver.Find(typeof(TModel));
        var idValue = idProperty?.GetValue(Model);
        var isNew = IsNewModel;

        foreach (var participant in SaveParticipants)
        {
            if (await participant.ValidateAsync())
                continue;

            StateHasChanged();
            return;
        }

        var saveContext = new CardSaveContext(Model!, isNew);

        if (OnBeforeSave.HasDelegate)
            await OnBeforeSave.InvokeAsync(Model);

        foreach (var participant in SaveParticipants)
            await participant.OnBeforeSaveAsync(saveContext);

        if (DeferSave)
        {
            var deferredChangedFields = GetChangedFields();

            if (OnDeferredSave.HasDelegate)
                await OnDeferredSave.InvokeAsync(deferredChangedFields);

            SnapshotOriginalValues();

            foreach (var participant in SaveParticipants)
                await participant.OnAfterSaveAsync(saveContext);

            ApplyParticipantMessage(saveContext);

            if (OnAfterSave.HasDelegate)
                await OnAfterSave.InvokeAsync(Model);

            return;
        }

        var provider = ResolvedDataProvider;

        try
        {
            if (isNew)
            {
                Model = await provider.CreateAsync(Model);
            }
            else
            {
                var changedFields = GetChangedFields();

                if (changedFields.Count > 0)
                    Model = await provider.PatchAsync(idValue!, changedFields, ConcurrencyStamp);
            }

            foreach (var (propertyName, listPart) in RegisteredListParts)
            {
                if (isNew && IsSentWithParent(propertyName))
                    continue;

                await listPart.FlushAsync(Model!);
            }

            SnapshotOriginalValues();
        }
        catch (ConcurrencyConflictException)
        {
            ErrorMessage = FrameworkLocalizer["ConcurrencyConflict"].Value;
            StateHasChanged();
            return;
        }
        catch (BaseValidationException ex)
        {
            ErrorMessage = ex.Message;
            StateHasChanged();
            return;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = FrameworkLocalizer["SaveFailed", DescribeFailure(ex)].Value;
            StateHasChanged();
            return;
        }

        foreach (var participant in SaveParticipants)
            await participant.OnAfterSaveAsync(saveContext);

        ApplyParticipantMessage(saveContext);

        if (OnAfterSave.HasDelegate)
            await OnAfterSave.InvokeAsync(Model);
    }

    private bool IsSentWithParent(string propertyName)
    {
        var listPart = ResolvedListParts.FirstOrDefault(part => part.PropertyName == propertyName);

        return listPart is not null && IsSerializedWithParent(ExpressionHelper.GetPropertyInfo(listPart.Property));
    }

    /// <summary>
    /// Whether the JSON payload of the parent carries the items of this collection. The generic endpoint stores a
    /// created parent together with everything it carries, so items sent along must not be created a second time.
    /// </summary>
    internal static bool IsSerializedWithParent(PropertyInfo property) =>
        property.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always };

    private static string DescribeFailure(HttpRequestException exception) =>
        exception.StatusCode is { } statusCode ? $"HTTP {(int)statusCode}" : exception.Message;

    private static string DescribeException(Exception exception) =>
        exception is HttpRequestException httpException ? DescribeFailure(httpException) : exception.Message;

    private void ApplyParticipantMessage(CardSaveContext context)
    {
        if (!string.IsNullOrEmpty(context.Message))
            ErrorMessage = context.Message;
    }

    private bool HasUnsavedChanges =>
        GetChangedFields().Count > 0
        || RegisteredListParts.Values.Any(listPart => listPart.HasPendingChanges);

    private Task TryCloseAsync() => ConfirmAndCloseAsync();

    /// <summary>
    /// Closes the card on behalf of the list when the browser is about to leave it, asking the same question the close
    /// button asks when there are unsaved changes.
    /// </summary>
    /// <returns>Whether the card closed; false when the user chose to keep editing or a question is already open.</returns>
    private async Task<bool> RequestGuardedCloseAsync()
    {
        if (IsConfirmDialogOpen)
            return false;

        var closed = await ConfirmAndCloseAsync();
        StateHasChanged();

        return closed;
    }

    private async Task<bool> ConfirmAndCloseAsync()
    {
        if (HasUnsavedChanges)
        {
            IsConfirmDialogOpen = true;
            bool discard;
            try
            {
                discard = await ResolvedConfirmationService.ConfirmAsync(
                    FrameworkLocalizer["ConfirmDiscardChanges"].Value,
                    FrameworkLocalizer["DiscardChangesTitle"].Value,
                    FrameworkLocalizer["Discard"].Value,
                    FrameworkLocalizer["KeepEditing"].Value);
            }
            finally
            {
                IsConfirmDialogOpen = false;
            }

            if (!discard)
                return false;

            RevertToSnapshot();
        }

        if (OnCancelled.HasDelegate)
            await OnCancelled.InvokeAsync();

        return true;
    }

    private void RevertToSnapshot()
    {
        ErrorMessage = null;

        foreach (var property in typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
                continue;

            if (NavigationPropertyNames.Contains(property.Name))
                continue;

            if (OriginalValues.TryGetValue(property.Name, out var originalValue))
                property.SetValue(Model, originalValue);
        }
    }

    private IConfirmationService ResolvedConfirmationService =>
        ServiceProvider.GetService<IConfirmationService>()
        ?? new FluentUiConfirmationService(ServiceProvider.GetRequiredService<IDialogService>());

    private RenderFragment RenderListPartItems(BaseListPartConfiguration<TModel> listPartConfig)
    {
        return builder =>
        {
            var propertyInfo = ExpressionHelper.GetPropertyInfo(listPartConfig.Property);
            var itemType = new[] { propertyInfo.PropertyType }
                .Concat(propertyInfo.PropertyType.GetInterfaces())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                .Select(i => i.GetGenericArguments()[0])
                .FirstOrDefault();

            if (itemType is null)
                return;

            if (listPartConfig.ListPartComponentType is not null)
            {
                var collection = propertyInfo.GetValue(Model);
                builder.OpenComponent(0, listPartConfig.ListPartComponentType);
                builder.AddAttribute(1, "Items", collection);
                builder.CloseComponent();
                return;
            }

            var componentType = typeof(BaseListPart<>).MakeGenericType(itemType);
            var items = propertyInfo.GetValue(Model);
            var registrationKey = listPartConfig.PropertyName;

            builder.OpenComponent(0, componentType);
            builder.AddAttribute(1, "Items", items);
            builder.AddAttribute(2, "AllowAdd", listPartConfig.AllowAdd && !ReadOnly);
            builder.AddAttribute(3, "AllowDelete", listPartConfig.AllowDelete && !ReadOnly);
            builder.AddAttribute(4, "AllowReorder", listPartConfig.AllowReorder && !ReadOnly);
            builder.AddAttribute(5, "Localizer", Localizer);
            builder.AddAttribute(6, "CardConfiguration", listPartConfig.ChildCardConfiguration);
            builder.AddAttribute(7, "CardType", listPartConfig.ChildCardType);
            builder.AddAttribute(8, "ReadOnly", ReadOnly);
            builder.AddComponentReferenceCapture(9, instance =>
            {
                if (instance is IListPartFlushable flushable)
                    RegisteredListParts[registrationKey] = flushable;
            });
            builder.CloseComponent();
        };
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
            return Activator.CreateInstance(type);

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        AssistantRegistration?.Dispose();
        AssistantRegistration = null;
        CloseGuard?.Unregister(this);

        try
        {
            if (DismissModule is not null && DismissToken is not null)
                await DismissModule.InvokeVoidAsync("unregister", DismissToken);

            if (DismissModule is not null)
                await DismissModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        SelfReference?.Dispose();
    }
}
