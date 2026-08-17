using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.Internal;

public partial class BasePropertyInput<TModel> : IDisposable
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    [CascadingParameter]
    private IFieldCollector<TModel>? FieldCollector { get; set; }

    [Parameter, EditorRequired]
    public TModel Model { get; set; } = default!;

    [Parameter, EditorRequired]
    public PropertyFieldConfig<TModel> FieldConfig { get; set; } = default!;

    [Parameter]
    public bool IsEditing { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public EventCallback<PropertyChangedEventArgs<TModel>> OnBeforePropertyChanged { get; set; }

    [Parameter]
    public EventCallback<PropertyChangedEventArgs<TModel>> OnAfterPropertyChanged { get; set; }

    private PropertyInfo? PropertyInfo { get; set; }
    private Type PropertyType { get; set; } = typeof(string);
    private object? CurrentValue => PropertyInfo?.GetValue(Model);

    private string FieldElementId { get; } = $"bpi-{Guid.NewGuid():N}";

    private string TooltipAnchorId { get; } = $"bpi-tip-{Guid.NewGuid():N}";

    private IStringLocalizer ResolvedLocalizer =>
        LocalizerResolver.ResolveProperty(ServiceProvider, Localizer, typeof(TModel));

    private IStringLocalizer FrameworkLocalizer =>
        LocalizerResolver.ResolveFramework(ServiceProvider);

    private string ResolvedLabel
    {
        get
        {
            if (FieldConfig.Label is not null)
                return FieldConfig.Label;

            var result = ResolvedLocalizer[FieldConfig.PropertyName];
            if (!result.ResourceNotFound)
                return result.Value;

            return FieldConfig.PropertyName;
        }
    }

    private string? ResolvedTooltip
    {
        get
        {
            if (FieldConfig.Tooltip is not null)
                return FieldConfig.Tooltip;

            return LocalizerResolver.ResolveOptional(ResolvedLocalizer, $"{FieldConfig.PropertyName}_Tooltip");
        }
    }

    private string? ResolvedPlaceholder
    {
        get
        {
            if (FieldConfig.Placeholder is not null)
                return FieldConfig.Placeholder;

            return LocalizerResolver.ResolveOptional(ResolvedLocalizer, $"{FieldConfig.PropertyName}_Placeholder");
        }
    }

    private bool IsNumericType => PropertyType == typeof(int) || PropertyType == typeof(int?)
        || PropertyType == typeof(long) || PropertyType == typeof(long?)
        || PropertyType == typeof(decimal) || PropertyType == typeof(decimal?)
        || PropertyType == typeof(double) || PropertyType == typeof(double?)
        || PropertyType == typeof(float) || PropertyType == typeof(float?);

    #region Navigation Property Support

    private bool NavigationResolved { get; set; }
    private bool IsNavigationProperty { get; set; }

    /// <summary>
    /// True when the CRUD-UI-02 read guard denied the current user Read access to the navigation
    /// target type. The field must never fall through to the editable string fallback in this case —
    /// the bound value's display text is still resolved so a read-only field can show it, but no
    /// lookup count/items are ever loaded from the target provider.
    /// </summary>
    private bool NavigationReadDenied { get; set; }
    private bool UseBrowseMode { get; set; }
    private NavigationPropertyInfo? NavigationInfo { get; set; }
    private PropertyInfo? ForeignKeyPropertyInfo { get; set; }
    private Type? NavigationType { get; set; }
    private object? NavigationDataProvider { get; set; }
    private string? ResolvedDisplayPropertyName { get; set; }
    private IReadOnlyList<string> ResolvedDisplayPropertyNames { get; set; } = [];
    private List<LookupItem> LookupItems { get; set; } = [];
    private string? SelectedDisplayText { get; set; }
    private bool ShowBrowsePanel { get; set; }
    private List<LookupItem> BrowseItems { get; set; } = [];
    private string? BrowseSearchText { get; set; }
    private int BrowseTotalCount { get; set; }
    private bool HasMoreBrowseItems => BrowseItems.Count < BrowseTotalCount;
    private string? CurrentFkStringValue => ForeignKeyPropertyInfo?.GetValue(Model)?.ToString();

    private sealed record LookupItem(string Id, string DisplayText);

    /// <summary>
    /// Key for the navigation FluentSelect. Bumped exactly once — after the options and an initial foreign-key value
    /// are both present (see <see cref="TryReinitNavigationSelect"/>) — to force Blazor to recreate the select with
    /// the value applied while the options already exist in the DOM. This works around FluentSelect dropping a value
    /// that was set in the same render its options first appeared. It is never bumped on user selections, so the
    /// element is not recreated mid-interaction (which would recurse the dialog focus trap).
    /// </summary>
    private int NavigationSelectGeneration { get; set; }
    private bool NavigationReinitialized { get; set; }
    private bool NavigationUserInteracted { get; set; }

    /// <summary>
    /// True for a reference-navigation field, determined synchronously (before the async options load) so the field
    /// can be recognised from the very first render. The owning card waits for every such field to finish loading
    /// before it reveals its content, so all fields appear at once instead of the lookup popping in late.
    /// </summary>
    private bool IsReferenceNavigationField { get; set; }

    private bool NavigationResolveCompleted { get; set; }
    private bool NavigationLoadReported { get; set; }

    /// <summary>
    /// A reference-navigation field is ready to be shown once its options have loaded and — when it already has a
    /// value — the select has been reinitialised so the value is applied. Fields without a value (new records) or in
    /// browse mode are ready as soon as resolution completes.
    /// </summary>
    private bool NavigationDisplayReady
    {
        get
        {
            if (!IsReferenceNavigationField)
                return true;

            if (!NavigationResolveCompleted)
                return false;

            if (UseBrowseMode || !IsNavigationProperty)
                return true;

            if (string.IsNullOrEmpty(CurrentFkStringValue))
                return true;

            return NavigationReinitialized;
        }
    }

    private void PrepareNavigationFieldKind()
    {
        if (ForeignKeyPropertyInfo is not null || PropertyInfo is null)
            return;

        var navigationInfo = NavigationPropertyResolver.GetNavigationProperty(typeof(TModel), PropertyInfo.Name);
        if (navigationInfo is null || navigationInfo.IsCollection)
            return;

        var foreignKey = NavigationPropertyResolver.GetForeignKeyProperty(typeof(TModel), PropertyInfo.Name);
        if (foreignKey is null)
            return;

        IsReferenceNavigationField = true;
        ForeignKeyPropertyInfo = foreignKey;
    }

    private void ReportNavigationLoadedIfReady()
    {
        if (NavigationLoadReported || !IsReferenceNavigationField)
            return;

        if (!NavigationDisplayReady)
            return;

        NavigationLoadReported = true;
        FieldCollector?.NotifyNavigationFieldLoaded();
    }

    private void TryReinitNavigationSelect()
    {
        if (NavigationReinitialized || NavigationUserInteracted)
            return;

        if (!IsNavigationProperty || UseBrowseMode)
            return;

        if (LookupItems.Count == 0 || string.IsNullOrEmpty(CurrentFkStringValue))
            return;

        NavigationReinitialized = true;
        NavigationSelectGeneration++;
        StateHasChanged();
    }

    #endregion

    protected override async Task OnParametersSetAsync()
    {
        PropertyInfo = ExpressionHelper.GetPropertyInfo(FieldConfig.Property);
        PropertyType = Nullable.GetUnderlyingType(PropertyInfo.PropertyType) ?? PropertyInfo.PropertyType;

        ResolveCustomInput();

        PrepareNavigationFieldKind();

        if (!NavigationResolved)
        {
            NavigationResolved = true;
            await ResolveNavigationAsync();
            NavigationResolveCompleted = true;
        }

        if (IsNavigationProperty || NavigationReadDenied)
            ResolveCurrentDisplayText();
    }

    #region Custom Input Resolution

    private static readonly Type NoCustomInputSentinel = typeof(NoCustomInputMarker);

    private sealed class NoCustomInputMarker;

    private Type? ResolvedCustomInput { get; set; }

    private Dictionary<string, object?>? CustomInputParameters { get; set; }

    private DynamicComponent? CustomInputRef { get; set; }

    private ICardSaveParticipant? RegisteredParticipant { get; set; }

    private bool HasExplicitTemplate =>
        (FieldConfig.EditorTemplate is not null && IsEditing)
        || (FieldConfig.DisplayTemplate is not null && !IsEditing);

    private void ResolveCustomInput()
    {
        if (PropertyInfo is null || HasExplicitTemplate)
        {
            ResolvedCustomInput = null;
            CustomInputParameters = null;
            return;
        }

        var cacheKey = (typeof(TModel), PropertyInfo.MetadataToken, IsEditing);

        var resolved = CustomPropertyResolutionCache.Inputs.GetOrAdd(cacheKey, _ => ResolveCustomInputType());

        if (ReferenceEquals(resolved, NoCustomInputSentinel))
        {
            ResolvedCustomInput = null;
            CustomInputParameters = null;
            return;
        }

        ResolvedCustomInput = resolved;
        CustomInputParameters = resolved is null ? null : BuildCustomInputParameters();
    }

    private Type ResolveCustomInputType()
    {
        var context = new CustomPropertyContext(typeof(TModel), PropertyInfo!, PropertyType, IsEditing);

        foreach (var candidate in ServiceProvider.GetServices<IBaseCustomPropertyInput>())
        {
            if (candidate.CanHandle(context))
                return candidate.GetType();
        }

        return NoCustomInputSentinel;
    }

    private Dictionary<string, object?> BuildCustomInputParameters()
    {
        return new Dictionary<string, object?>
        {
            [nameof(IBaseCustomPropertyInput.Model)] = Model,
            [nameof(IBaseCustomPropertyInput.Property)] = PropertyInfo,
            [nameof(IBaseCustomPropertyInput.Value)] = CurrentValue,
            [nameof(IBaseCustomPropertyInput.ValueChanged)] = EventCallback.Factory.Create<object?>(this, OnValueChangedAsync),
            [nameof(IBaseCustomPropertyInput.IsEditing)] = IsEditing,
            [nameof(IBaseCustomPropertyInput.ReadOnly)] = !IsEditing,
            [nameof(IBaseCustomPropertyInput.Localizer)] = ResolvedLocalizer
        };
    }

    protected override void OnAfterRender(bool firstRender)
    {
        SyncSaveParticipant();
        TryReinitNavigationSelect();
        ReportNavigationLoadedIfReady();
    }

    private void SyncSaveParticipant()
    {
        var participant = CustomInputRef?.Instance as ICardSaveParticipant;

        if (ReferenceEquals(participant, RegisteredParticipant))
            return;

        if (RegisteredParticipant is not null)
            FieldCollector?.UnregisterSaveParticipant(RegisteredParticipant);

        RegisteredParticipant = participant;

        if (participant is not null)
            FieldCollector?.RegisterSaveParticipant(participant);
    }

    public void Dispose()
    {
        if (RegisteredParticipant is not null)
            FieldCollector?.UnregisterSaveParticipant(RegisteredParticipant);
    }

    #endregion

    #region Access Rights

    private ClaimsPrincipal? CurrentUser { get; set; }

    private async Task LoadCurrentUserAsync()
    {
        var authenticationStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (authenticationStateProvider is null)
            return;

        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        CurrentUser = authenticationState.User;
    }

    #endregion

    #region Navigation Resolution

    private async Task ResolveNavigationAsync()
    {
        if (PropertyInfo is null)
            return;

        NavigationInfo = NavigationPropertyResolver.GetNavigationProperty(typeof(TModel), PropertyInfo.Name);
        if (NavigationInfo is null || NavigationInfo.IsCollection)
            return;

        NavigationType = NavigationInfo.TargetType;

        ForeignKeyPropertyInfo = NavigationPropertyResolver.GetForeignKeyProperty(typeof(TModel), PropertyInfo.Name);
        if (ForeignKeyPropertyInfo is null)
            return;

        ResolvedDisplayPropertyNames = Display.DisplayKeyResolver.ResolveLookupDisplayPropertyNames(
            NavigationType, FieldConfig.DisplayPropertyName);
        ResolvedDisplayPropertyName = ResolvedDisplayPropertyNames.FirstOrDefault();

        await LoadCurrentUserAsync();

        if (CurrentUser is not null
            && !CrudAccessResolver.EvaluateNavigationTarget(typeof(TModel), PropertyInfo.Name, NavigationType, CurrentUser).HasFlag(CrudRights.Read))
        {
            NavigationReadDenied = true;
            ResolveCurrentDisplayText();
            return;
        }

        var providerType = typeof(IBaseDataProvider<>).MakeGenericType(NavigationType);
        NavigationDataProvider = ServiceProvider.GetService(providerType);
        if (NavigationDataProvider is null)
            return;

        IsNavigationProperty = true;

        var count = await GetCountViaReflectionAsync();
        UseBrowseMode = count > FieldConfig.LookupThreshold;

        if (!UseBrowseMode)
        {
            var (items, _) = await LoadItemsViaReflectionAsync(FieldConfig.LookupThreshold + 100);
            LookupItems = items;
        }
    }

    private void ResolveCurrentDisplayText()
    {
        var fkValue = CurrentFkStringValue;
        if (string.IsNullOrEmpty(fkValue))
        {
            SelectedDisplayText = null;
            return;
        }

        var match = LookupItems.FirstOrDefault(i => i.Id == fkValue);
        if (match is not null)
        {
            SelectedDisplayText = match.DisplayText;
            return;
        }

        var navValue = PropertyInfo?.GetValue(Model);
        if (navValue is not null)
        {
            var navDisplay = Display.DisplayKeyResolver.BuildDisplayString(
                navValue, ResolvedDisplayPropertyNames, Display.DisplayKeyResolver.DefaultSeparator);
            SelectedDisplayText = string.IsNullOrEmpty(navDisplay) ? navValue.ToString() : navDisplay;
            return;
        }

        SelectedDisplayText = fkValue;
    }

    private async Task<int> GetCountViaReflectionAsync()
    {
        var providerType = typeof(IBaseDataProvider<>).MakeGenericType(NavigationType!);
        var method = providerType.GetMethod(nameof(IBaseDataProvider<object>.GetCountAsync))!;
        var task = (Task)method.Invoke(NavigationDataProvider!, [null, CancellationToken.None])!;
        await task;
        return (int)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private async Task<(List<LookupItem> Items, int TotalCount)> LoadItemsViaReflectionAsync(
        int take, string? searchText = null, int skip = 0)
    {
        var providerType = typeof(IBaseDataProvider<>).MakeGenericType(NavigationType!);
        var getListMethod = providerType.GetMethod(nameof(IBaseDataProvider<object>.GetListAsync))!;

        var query = new BaseQuery { Take = take, Skip = skip };

        if (!string.IsNullOrWhiteSpace(searchText) && ResolvedDisplayPropertyName is not null)
        {
            query.Filters.Add(new FilterDescriptor
            {
                PropertyName = ResolvedDisplayPropertyName,
                Operator = FilterOperator.Contains,
                Value = searchText
            });
        }

        var task = (Task)getListMethod.Invoke(NavigationDataProvider!, [query, null, CancellationToken.None])!;
        await task;

        var resultProp = task.GetType().GetProperty("Result")!;
        var queryResult = resultProp.GetValue(task)!;

        var totalCount = (int)queryResult.GetType().GetProperty("TotalCount")!.GetValue(queryResult)!;
        var items = (IList)queryResult.GetType().GetProperty("Items")!.GetValue(queryResult)!;

        var idProp = NavigationType!.GetProperty("Id")!;

        var lookupItems = new List<LookupItem>(items.Count);
        foreach (var item in items)
        {
            var id = idProp.GetValue(item)?.ToString() ?? "";
            var display = Display.DisplayKeyResolver.BuildDisplayString(
                item, ResolvedDisplayPropertyNames, Display.DisplayKeyResolver.DefaultSeparator);

            if (string.IsNullOrEmpty(display))
                display = item?.ToString() ?? "";

            lookupItems.Add(new LookupItem(id, display));
        }

        return (lookupItems, totalCount);
    }

    #endregion

    #region Navigation Event Handlers

    private async Task OnNavigationSelectChangedAsync(string? selectedId)
    {
        NavigationUserInteracted = true;

        if (string.IsNullOrEmpty(selectedId))
        {
            await SetForeignKeyValueAsync(null, null);
            return;
        }

        var selected = LookupItems.FirstOrDefault(i => i.Id == selectedId);
        await SetForeignKeyValueAsync(selectedId, selected?.DisplayText);
    }

    private async Task OnBrowseItemSelectedAsync(LookupItem item)
    {
        await SetForeignKeyValueAsync(item.Id, item.DisplayText);
        ShowBrowsePanel = false;
    }

    private async Task SetForeignKeyValueAsync(string? idString, string? displayText)
    {
        if (ForeignKeyPropertyInfo is null)
            return;

        var oldValue = ForeignKeyPropertyInfo.GetValue(Model);
        object? newValue = null;

        if (idString is not null)
        {
            var fkType = Nullable.GetUnderlyingType(ForeignKeyPropertyInfo.PropertyType)
                ?? ForeignKeyPropertyInfo.PropertyType;
            newValue = ConvertForeignKeyValue(idString, fkType);
        }

        var beforeArgs = new PropertyChangedEventArgs<TModel>
        {
            Model = Model,
            PropertyName = ForeignKeyPropertyInfo.Name,
            OldValue = oldValue,
            NewValue = newValue
        };

        if (OnBeforePropertyChanged.HasDelegate)
        {
            await OnBeforePropertyChanged.InvokeAsync(beforeArgs);

            if (beforeArgs.Cancel)
                return;
        }

        ForeignKeyPropertyInfo.SetValue(Model, newValue);
        SelectedDisplayText = displayText;

        if (OnAfterPropertyChanged.HasDelegate)
            await OnAfterPropertyChanged.InvokeAsync(beforeArgs);
    }

    private static object ConvertForeignKeyValue(string idString, Type foreignKeyType)
    {
        if (foreignKeyType == typeof(Guid))
            return Guid.Parse(idString);

        if (foreignKeyType == typeof(string))
            return idString;

        return Convert.ChangeType(idString, foreignKeyType, CultureInfo.InvariantCulture);
    }

    private void ToggleBrowsePanel()
    {
        ShowBrowsePanel = !ShowBrowsePanel;

        if (ShowBrowsePanel && BrowseItems.Count == 0)
            _ = LoadBrowseItemsAsync();
    }

    private async Task LoadBrowseItemsAsync(bool reset = true)
    {
        var skip = reset ? 0 : BrowseItems.Count;
        var (items, totalCount) = await LoadItemsViaReflectionAsync(50, BrowseSearchText, skip);
        BrowseTotalCount = totalCount;

        if (reset)
            BrowseItems = items;
        else
            BrowseItems.AddRange(items);

        StateHasChanged();
    }

    private async Task OnBrowseSearchAsync()
    {
        await LoadBrowseItemsAsync();
    }

    private async Task LoadMoreBrowseItemsAsync()
    {
        await LoadBrowseItemsAsync(reset: false);
    }

    #endregion

    private async Task OnValueChangedAsync(object? newValue)
    {
        if (PropertyInfo is null)
            return;

        var oldValue = PropertyInfo.GetValue(Model);

        var beforeArgs = new PropertyChangedEventArgs<TModel>
        {
            Model = Model,
            PropertyName = FieldConfig.PropertyName,
            OldValue = oldValue,
            NewValue = newValue
        };

        if (OnBeforePropertyChanged.HasDelegate)
        {
            await OnBeforePropertyChanged.InvokeAsync(beforeArgs);

            if (beforeArgs.Cancel)
                return;
        }

        PropertyInfo.SetValue(Model, newValue);

        if (OnAfterPropertyChanged.HasDelegate)
            await OnAfterPropertyChanged.InvokeAsync(beforeArgs);
    }

    #region Value Adapters

    private string? StringValue
    {
        get => CurrentValue?.ToString();
        set => _ = OnValueChangedAsync(value);
    }

    private bool BoolValue
    {
        get => CurrentValue is true;
        set => _ = OnValueChangedAsync(value);
    }

    private DateTime? DateValue
    {
        get => CurrentValue switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ => null
        };
        set
        {
            object? converted = value;

            if (PropertyInfo?.PropertyType == typeof(DateTimeOffset) || PropertyInfo?.PropertyType == typeof(DateTimeOffset?))
                converted = value.HasValue ? new DateTimeOffset(value.Value) : null;

            _ = OnValueChangedAsync(converted);
        }
    }

    private string? NumericStringValue
    {
        get => CurrentValue?.ToString();
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _ = OnValueChangedAsync(null);
                return;
            }

            try
            {
                var converted = Convert.ChangeType(value, PropertyType);
                _ = OnValueChangedAsync(converted);
            }
            catch
            {
            }
        }
    }

    #endregion
}
