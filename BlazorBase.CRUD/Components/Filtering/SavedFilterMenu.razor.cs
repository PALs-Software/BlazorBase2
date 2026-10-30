using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.Filtering;

/// <summary>
/// The "Saved filters" section of the filter drawer: offers the code presets, lists the user's saved filters and lets
/// the user load, save (create, overwrite, rename), delete and default them.
/// </summary>
public partial class SavedFilterMenu
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    #region Parameters

    [Parameter, EditorRequired]
    public SavedFilterContext Saved { get; set; } = default!;

    /// <summary>
    /// The filter currently being edited in the drawer: what "save" stores and what is marked as active.
    /// </summary>
    [Parameter, EditorRequired]
    public FilterGroupModel Current { get; set; } = default!;

    /// <summary>
    /// Raised with a cleaned copy of the filter the user chose to load.
    /// </summary>
    [Parameter]
    public EventCallback<FilterGroupModel> OnLoad { get; set; }

    #endregion

    #region Members

    private readonly string BodyId = $"saved-filters-{Guid.NewGuid():N}";
    private IStringLocalizer? ResolvedLocalizer;
    private List<SavedListFilterInfo> Items = [];
    private bool Expanded;
    private bool Loading;
    private bool Busy;
    private string? Error;
    private string NewName = String.Empty;
    private string EditName = String.Empty;
    private Guid? EditingId;
    private Guid? ConfirmDeleteId;

    #endregion

    private IStringLocalizer Localizer =>
        ResolvedLocalizer ??= ServiceProvider.GetService<IStringLocalizerFactory>()?.Create(typeof(SavedFilterResources))
            ?? EmptyLocalizer.Instance;

    private bool HasUserDefault => Items.Any(item => item.IsDefault);

    private bool NameExists =>
        Items.Any(item => String.Equals(item.Name, NewName.Trim(), StringComparison.OrdinalIgnoreCase));

    private string T(string key) => Localizer[key].Value;

    private string T(string key, string argument) => Localizer[key, argument].Value;

    private async Task ToggleAsync()
    {
        Expanded = !Expanded;

        if (Expanded && Saved.Store is not null)
            await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        Loading = true;
        Error = null;

        try
        {
            Items = [.. await Saved.Store!.GetAsync(Saved.ListKey)];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = T("SavedFiltersUnavailable");
        }
        finally
        {
            Loading = false;
        }
    }

    private bool IsCurrent(FilterGroupModel filter) =>
        ListFilterSerializer.Canonicalize(FilterModelPruner.Prune(filter, Saved.Fields))
        == ListFilterSerializer.Canonicalize(FilterModelPruner.Prune(Current, Saved.Fields));

    private async Task LoadAsync(FilterGroupModel filter) =>
        await OnLoad.InvokeAsync(FilterModelPruner.Prune(filter, Saved.Fields));

    private async Task SaveAsync()
    {
        if (String.IsNullOrWhiteSpace(NewName))
            return;

        var input = new SavedListFilterInput
        {
            Name = NewName,
            Filter = FilterModelPruner.Prune(Current, Saved.Fields)
        };

        await RunAsync(async () =>
        {
            await Saved.Store!.SaveAsync(Saved.ListKey, input);
            NewName = String.Empty;
        });
    }

    private async Task OnSaveKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
            await SaveAsync();
    }

    private void BeginEdit(SavedListFilterInfo item)
    {
        ConfirmDeleteId = null;
        EditingId = item.Id;
        EditName = item.Name;
    }

    private void BeginDelete(SavedListFilterInfo item)
    {
        EditingId = null;
        ConfirmDeleteId = item.Id;
    }

    private void CancelEdit()
    {
        EditingId = null;
        ConfirmDeleteId = null;
    }

    private async Task OnEditKeyDownAsync(KeyboardEventArgs args, SavedListFilterInfo item)
    {
        if (args.Key == "Enter")
            await RenameAsync(item);
        else if (args.Key == "Escape")
            CancelEdit();
    }

    private async Task RenameAsync(SavedListFilterInfo item)
    {
        if (String.IsNullOrWhiteSpace(EditName))
            return;

        var input = new SavedListFilterInput { Id = item.Id, Name = EditName, Filter = item.Filter };

        await RunAsync(async () =>
        {
            await Saved.Store!.SaveAsync(Saved.ListKey, input);
            EditingId = null;
        });
    }

    private async Task DeleteAsync(SavedListFilterInfo item) =>
        await RunAsync(async () =>
        {
            await Saved.Store!.DeleteAsync(Saved.ListKey, item.Id);
            ConfirmDeleteId = null;
        });

    private async Task ToggleDefaultAsync(SavedListFilterInfo item) =>
        await RunAsync(async () =>
        {
            if (!await Saved.Store!.SetDefaultAsync(Saved.ListKey, item.IsDefault ? null : item.Id))
                Error = T("SavedFiltersGone");
        });

    /// <summary>
    /// Runs a store operation, then refreshes the list; turns the failures a user can cause into readable messages.
    /// </summary>
    private async Task RunAsync(Func<Task> operation)
    {
        Busy = true;
        Error = null;

        try
        {
            await operation();
        }
        catch (ListFilterNameConflictException)
        {
            Error = T("SavedFiltersNameExists");
        }
        catch (ListFilterValidationException)
        {
            Error = T("SavedFiltersInvalid");
        }
        catch (KeyNotFoundException)
        {
            Error = T("SavedFiltersGone");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = T("SavedFiltersUnavailable");
        }

        var failure = Error;
        await ReloadAsync();
        Error ??= failure;
        Busy = false;
    }
}
