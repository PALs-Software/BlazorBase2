using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Display;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

public partial class BaseListPart<TModel> : ComponentBase, IListPartFlushable where TModel : class, new()
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    #endregion

    [Parameter]
    public ICollection<TModel>? Items { get; set; }

    [Parameter]
    public bool AllowAdd { get; set; } = true;

    [Parameter]
    public bool AllowDelete { get; set; } = true;

    [Parameter]
    public bool AllowReorder { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public IBaseDataProvider<TModel>? DataProvider { get; set; }

    [Parameter]
    public BaseCardConfiguration<TModel>? CardConfiguration { get; set; }

    /// <summary>
    /// Optional custom card component rendered in the per-item edit dialog (defer-save mode),
    /// analogous to <c>CardType</c> on <c>BaseList</c>. Applies to <see cref="ListPartEditMode.Dialog"/>.
    /// </summary>
    [Parameter]
    public Type? CardType { get; set; }

    [Parameter]
    public ListPartEditMode EditMode { get; set; } = ListPartEditMode.Dialog;

    [Parameter]
    public string? ParentForeignKeyPropertyName { get; set; }

    [Parameter]
    public List<CrudActionGroup<TModel>>? ActionGroups { get; set; }

    private bool HasListPartActions =>
        ActionGroups is not null && ActionGroups.Any(g => g.Contexts.HasFlag(CrudActionContext.ListPart));

    #region Access Rights

    private ClaimsPrincipal? CurrentUser { get; set; }

    private CrudRights ClassRightsForCurrentUser =>
        CurrentUser is null ? CrudRights.All : CrudAccessResolver.EvaluateClass(typeof(TModel), CurrentUser);

    private bool CanAdd => AllowAdd && (ClassRightsForCurrentUser & CrudRights.Insert) == CrudRights.Insert;

    private bool CanDelete => AllowDelete && (ClassRightsForCurrentUser & CrudRights.Delete) == CrudRights.Delete;

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
        if (!field.Editable)
            return false;

        if (CurrentUser is null)
            return true;

        var effective = CrudAccessResolver.EvaluateProperty(typeof(TModel), field.PropertyName, CurrentUser);
        effective = CrudAccessResolver.ApplyViewRights(effective, field.AccessRules, CurrentUser);
        return (effective & CrudRights.Modify) == CrudRights.Modify;
    }

    #endregion

    protected override async Task OnInitializedAsync()
    {
        var authStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (authStateProvider is null)
            return;

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        CurrentUser = authState.User;
    }

    private IStringLocalizer FrameworkLocalizer =>
        LocalizerResolver.ResolveFramework(ServiceProvider);

    /// <summary>
    /// The text shown for one row of the list part.
    /// </summary>
    /// <remarks>
    /// This used to be a plain <c>item.ToString()</c>, which meant an entity that does not override
    /// <c>ToString</c> - the normal case for an EF entity - displayed its fully qualified CLR type
    /// name to the end user, identically for every row. The framework already knows how to name an
    /// entity: <see cref="DisplayKeyResolver"/> backs the <c>BaseCard</c> header and the FK lookup
    /// text, so using it here makes all three surfaces agree instead of leaving this one to a
    /// default that was never meant to be user-facing.
    ///
    /// Precedence is most-deliberate-first: the display-key attributes, then a conventional name
    /// property, then an overridden <c>ToString</c>, then the primary key, then a localized
    /// placeholder. It is spelled out here rather than delegated wholesale to
    /// <c>ResolveLookupDisplayPropertyNames</c> because that helper ends at the primary key, which
    /// would let a raw GUID outrank a <c>ToString</c> the type's author wrote on purpose - barely
    /// an improvement on the type name for someone reading the screen. The primary key still comes
    /// before giving up: for a saved row with nothing else to show, it is at least distinguishing.
    ///
    /// The default <c>ToString</c> is never used, because its output is exactly the string this
    /// method exists to stop showing.
    /// </remarks>
    private string ResolveItemLabel(TModel? item)
    {
        if (item is null)
            return string.Empty;

        var itemType = item.GetType();

        var attributeKeys = DisplayKeyResolver.GetAttributeDisplayKeyPropertyNames(itemType);
        if (BuildLabel(item, attributeKeys) is { Length: > 0 } fromAttributes)
            return fromAttributes;

        var conventionalName = DisplayKeyResolver.FindConventionalDisplayPropertyName(itemType);
        var primaryKeyName = DisplayKeyResolver.GetPrimaryKeyPropertyName(itemType);

        if (!string.Equals(conventionalName, primaryKeyName, StringComparison.Ordinal)
            && BuildLabel(item, [conventionalName]) is { Length: > 0 } fromConvention)
            return fromConvention;

        if (OverridesToString(itemType) && item.ToString() is { Length: > 0 } overridden)
            return overridden;

        // A type that declares display keys has already said how it wants to be identified. When
        // every one of them is empty on this particular row, falling back to the primary key puts a
        // bare database id on screen — "1" sitting next to "LOB-001 · Legend of Blue Eyes White
        // Dragon" — which names nothing and only leaks a key. A type that declares no display key at
        // all is the one taken to mean its key is its identity.
        if (attributeKeys.Count == 0 && BuildLabel(item, [primaryKeyName]) is { Length: > 0 } fromKey)
            return fromKey;

        return FrameworkLocalizer["UnnamedListItem"];
    }

    private static string BuildLabel(TModel item, IReadOnlyList<string> propertyNames) =>
        propertyNames.Count == 0
            ? string.Empty
            : DisplayKeyResolver.BuildDisplayString(item, propertyNames, DisplayKeyResolver.DefaultSeparator);

    private static bool OverridesToString(Type type) =>
        type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)?.DeclaringType != typeof(object);

    private IBaseDataProvider<TModel> ResolvedProvider =>
        DataProvider ?? ServiceProvider.GetService<IBaseDataProvider<TModel>>()
        ?? throw new InvalidOperationException($"No DataProvider available for {typeof(TModel).Name}. Register via DI or pass as parameter.");

    #region Events

    [Parameter]
    public EventCallback<ItemEventArgs<TModel>> OnBeforeItemAdded { get; set; }

    [Parameter]
    public EventCallback<TModel> OnAfterItemAdded { get; set; }

    [Parameter]
    public EventCallback<ItemEventArgs<TModel>> OnBeforeItemRemoved { get; set; }

    [Parameter]
    public EventCallback<TModel> OnAfterItemRemoved { get; set; }

    #endregion

    #region Pending State

    private HashSet<TModel> PendingAdds { get; } = new(ReferenceEqualityComparer.Instance);
    private HashSet<TModel> PendingRemoves { get; } = new(ReferenceEqualityComparer.Instance);
    private Dictionary<TModel, Dictionary<string, object?>> PendingUpdates { get; } =
        new(ReferenceEqualityComparer.Instance);

    private static readonly PropertyInfo? IdProperty =
        typeof(TModel).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

    public bool HasPendingChanges =>
        PendingAdds.Count > 0 || PendingRemoves.Count > 0 || PendingUpdates.Count > 0;

    #endregion

    private async Task OnAddItemAsync()
    {
        var newItem = new TModel();
        var eventArgs = new ItemEventArgs<TModel> { Item = newItem };

        if (OnBeforeItemAdded.HasDelegate)
        {
            await OnBeforeItemAdded.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        Items?.Add(newItem);
        PendingAdds.Add(newItem);

        if (OnAfterItemAdded.HasDelegate)
            await OnAfterItemAdded.InvokeAsync(newItem);

        if (EditMode == ListPartEditMode.Dialog)
            await OpenEditDialogAsync(newItem);

        StateHasChanged();
    }

    private async Task OnRemoveItemAsync(TModel item)
    {
        var eventArgs = new ItemEventArgs<TModel> { Item = item };

        if (OnBeforeItemRemoved.HasDelegate)
        {
            await OnBeforeItemRemoved.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        Items?.Remove(item);

        if (PendingAdds.Remove(item))
        {
            PendingUpdates.Remove(item);
        }
        else
        {
            PendingUpdates.Remove(item);
            PendingRemoves.Add(item);
        }

        if (OnAfterItemRemoved.HasDelegate)
            await OnAfterItemRemoved.InvokeAsync(item);

        StateHasChanged();
    }

    private void OnInlinePropertyChanged(TModel item, PropertyChangedEventArgs<TModel> args)
    {
        TrackChangedField(item, args.PropertyName, args.NewValue);
    }

    private void TrackChangedField(TModel item, string propertyName, object? newValue)
    {
        if (PendingAdds.Contains(item))
            return;

        if (!PendingUpdates.TryGetValue(item, out var fields))
        {
            fields = new Dictionary<string, object?>();
            PendingUpdates[item] = fields;
        }

        fields[propertyName] = newValue;
    }

    private async Task OpenEditDialogAsync(TModel item)
    {
        var dialogData = new BaseDialogData<TModel>
        {
            Model = item,
            IsNew = PendingAdds.Contains(item),
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        var dialogParameters = new DialogParameters
        {
            Title = string.Empty,
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            Width = "600px",
            PreventDismissOnOverlayClick = true,
            ShowDismiss = false
        };

        var dialog = await DialogService.ShowDialogAsync<BaseListPartItemDialog<TModel>>(dialogData, dialogParameters);
        var result = await dialog.Result;

        if (result is { Cancelled: false, Data: Dictionary<string, object?> changedFields })
        {
            if (!PendingAdds.Contains(item))
                MergeChangedFields(item, changedFields);
        }
        else if (result.Cancelled && dialogData.IsNew)
        {
            Items?.Remove(item);
            PendingAdds.Remove(item);
        }

        StateHasChanged();
    }

    private void MergeChangedFields(TModel item, Dictionary<string, object?> changedFields)
    {
        if (changedFields.Count == 0)
            return;

        if (!PendingUpdates.TryGetValue(item, out var existing))
        {
            existing = new Dictionary<string, object?>();
            PendingUpdates[item] = existing;
        }

        foreach (var (key, value) in changedFields)
            existing[key] = value;
    }

    public async Task FlushAsync(object parentEntity, CancellationToken cancellationToken = default)
    {
        var provider = ResolvedProvider;
        var fkProperty = ResolveParentForeignKey(parentEntity.GetType());
        var parentIdProperty = parentEntity.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        var parentId = parentIdProperty?.GetValue(parentEntity);

        foreach (var removed in PendingRemoves.ToList())
        {
            var id = IdProperty?.GetValue(removed);

            if (id is null || IsDefaultId(id))
                continue;

            await provider.DeleteAsync(id, cancellationToken);
        }
        PendingRemoves.Clear();

        foreach (var added in PendingAdds.ToList())
        {
            if (fkProperty is not null && parentId is not null)
            {
                var convertedId = ConvertToFkType(parentId, fkProperty.PropertyType);
                fkProperty.SetValue(added, convertedId);
            }

            var created = await provider.CreateAsync(added, cancellationToken);
            ReplaceItem(added, created);
        }
        PendingAdds.Clear();

        foreach (var (item, fields) in PendingUpdates.ToList())
        {
            var id = IdProperty?.GetValue(item);

            if (id is null || IsDefaultId(id) || fields.Count == 0)
                continue;

            var sanitized = fields
                .Where(kvp => typeof(TModel).GetProperty(kvp.Key) is not null
                              && NavigationPropertyResolver.GetNavigationProperty(typeof(TModel), kvp.Key) is null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (sanitized.Count > 0)
                await provider.PatchAsync(id, sanitized, concurrencyStamp: null, cancellationToken);
        }
        PendingUpdates.Clear();
    }

    private PropertyInfo? ResolveParentForeignKey(Type parentType)
    {
        if (!string.IsNullOrEmpty(ParentForeignKeyPropertyName))
            return typeof(TModel).GetProperty(ParentForeignKeyPropertyName, BindingFlags.Public | BindingFlags.Instance);

        var navToParent = NavigationPropertyResolver.GetNavigationProperties(typeof(TModel))
            .FirstOrDefault(n => !n.IsCollection && n.TargetType == parentType);

        if (navToParent?.ForeignKeyPropertyName is not null)
            return typeof(TModel).GetProperty(navToParent.ForeignKeyPropertyName, BindingFlags.Public | BindingFlags.Instance);

        var conventional = typeof(TModel).GetProperty($"{parentType.Name}Id", BindingFlags.Public | BindingFlags.Instance);
        if (conventional is not null)
            return conventional;

        throw new InvalidOperationException(
            $"Could not resolve the foreign key on {typeof(TModel).Name} pointing to {parentType.Name}. " +
            $"Set ParentForeignKeyPropertyName explicitly on the BaseListPart.");
    }

    private static object ConvertToFkType(object id, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (id.GetType() == underlying)
            return id;

        if (underlying == typeof(Guid) && id is string guidString)
            return Guid.Parse(guidString);

        return Convert.ChangeType(id, underlying);
    }

    private static bool IsDefaultId(object id)
    {
        var type = id.GetType();

        if (!type.IsValueType)
            return false;

        return id.Equals(Activator.CreateInstance(type));
    }

    private void ReplaceItem(TModel original, TModel replacement)
    {
        if (Items is null || ReferenceEquals(original, replacement))
            return;

        if (Items is IList<TModel> list)
        {
            var index = -1;
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], original))
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                list[index] = replacement;
                return;
            }
        }

        Items.Remove(original);
        Items.Add(replacement);
    }
}
