using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Icon = Microsoft.FluentUI.AspNetCore.Components.Icon;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Fluent builder for configuring a CRUD action group containing related actions.
/// </summary>
public class CrudActionGroupBuilder<TModel> where TModel : class
{
    private readonly CrudActionGroup<TModel> config;
    private int orderCounter;

    public CrudActionGroupBuilder(string name)
    {
        config = new CrudActionGroup<TModel> { Name = name };
    }

    public CrudActionGroupBuilder<TModel> Caption(string caption) { config.Caption = caption; return this; }
    public CrudActionGroupBuilder<TModel> Icon(Icon icon) { config.Icon = icon; return this; }
    public CrudActionGroupBuilder<TModel> ShowIn(CrudActionContext contexts) { config.Contexts = contexts; return this; }
    public CrudActionGroupBuilder<TModel> Order(int order) { config.Order = order; return this; }
    public CrudActionGroupBuilder<TModel> Visible(Func<CrudActionVisibilityArgs<TModel>, Task<bool>> predicate) { config.Visible = predicate; return this; }
    public CrudActionGroupBuilder<TModel> VisibleForRoles(Func<ClaimsPrincipal, bool> predicate) { config.VisibleForRoles = predicate; return this; }

    public CrudActionGroupBuilder<TModel> Action(string caption, Action<CrudActionBuilder<TModel>> configure)
    {
        var builder = new CrudActionBuilder<TModel>();
        builder.Caption(caption);
        builder.Order(orderCounter++);
        configure(builder);
        config.Actions.Add(builder.Build());
        return this;
    }

    internal CrudActionGroup<TModel> Build() => config;
}
