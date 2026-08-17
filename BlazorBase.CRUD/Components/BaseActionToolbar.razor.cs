using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components;

public partial class BaseActionToolbar<TModel> : ComponentBase where TModel : class
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    [Parameter]
    public List<CrudActionGroup<TModel>> ActionGroups { get; set; } = [];

    [Parameter]
    public CrudActionContext Context { get; set; }

    [Parameter]
    public TModel? Item { get; set; }

    [Parameter]
    public IReadOnlyCollection<TModel> SelectedItems { get; set; } = [];

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public EventCallback OnActionExecuted { get; set; }

    private RenderFragment? DynamicComponentFragment { get; set; }
    private ClaimsPrincipal? CurrentUser { get; set; }

    private List<CrudActionGroup<TModel>> VisibleGroups =>
        ActionGroups
            .Where(g => g.Contexts.HasFlag(Context))
            .Where(g => IsGroupVisible(g))
            .Where(g => g.Actions.Any(a => a.Contexts.HasFlag(Context) && IsActionVisible(a)))
            .OrderBy(g => g.Order)
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        var authStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (authStateProvider is not null)
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            CurrentUser = authState.User;
        }
    }

    private bool IsGroupVisible(CrudActionGroup<TModel> group)
    {
        if (group.VisibleForRoles is not null && CurrentUser is not null && !group.VisibleForRoles(CurrentUser))
            return false;

        return true;
    }

    private bool IsActionVisible(CrudAction<TModel> action)
    {
        if (!action.Contexts.HasFlag(Context))
            return false;

        if (action.VisibleForRoles is not null && CurrentUser is not null && !action.VisibleForRoles(CurrentUser))
            return false;

        return true;
    }

    private bool IsActionEnabled(CrudAction<TModel> action)
    {
        if (action.IsBulkAction && SelectedItems.Count == 0)
            return false;

        return true;
    }

    private string ResolveText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (Localizer is not null)
            return Localizer[text]?.Value ?? text;

        return text;
    }

    private async Task InvokeActionAsync(CrudAction<TModel> action)
    {
        if (action.DynamicComponent is not null)
        {
            RenderDynamicComponent(action);
            return;
        }

        if (action.Action is null)
            return;

        var eventArgs = new CrudActionEventArgs<TModel>
        {
            Item = Item,
            SelectedItems = action.IsBulkAction ? SelectedItems : (Item is not null ? [Item] : []),
            ServiceProvider = ServiceProvider,
            Context = Context
        };

        await action.Action(eventArgs);

        if (OnActionExecuted.HasDelegate)
            await OnActionExecuted.InvokeAsync();
    }

    private void RenderDynamicComponent(CrudAction<TModel> action)
    {
        var componentConfig = action.DynamicComponent!;

        var eventArgs = new CrudActionEventArgs<TModel>
        {
            Item = Item,
            SelectedItems = action.IsBulkAction ? SelectedItems : (Item is not null ? [Item] : []),
            ServiceProvider = ServiceProvider,
            Context = Context
        };

        DynamicComponentFragment = builder =>
        {
            builder.OpenComponent(0, componentConfig.ComponentType);

            if (componentConfig.Parameters is not null)
            {
                foreach (var parameter in componentConfig.Parameters)
                    builder.AddComponentParameter(1, parameter.Key, parameter.Value);
            }

            builder.AddComponentParameter(2, "Args", eventArgs);
            builder.AddComponentParameter(3, "OnClose",
                EventCallback.Factory.Create(this, async () =>
                {
                    DynamicComponentFragment = null;

                    if (componentConfig.OnComponentClosed is not null)
                        await componentConfig.OnComponentClosed(eventArgs);

                    if (OnActionExecuted.HasDelegate)
                        await OnActionExecuted.InvokeAsync();

                    StateHasChanged();
                }));

            builder.CloseComponent();
        };

        StateHasChanged();
    }
}
