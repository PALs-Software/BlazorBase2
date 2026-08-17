using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Implemented by parent components (BaseList, BaseCard) to collect declaratively defined action groups.
/// </summary>
internal interface IActionGroupCollector<TModel> where TModel : class
{
    void AddActionGroup(CrudActionGroup<TModel> group);
}

/// <summary>
/// Implemented by BaseActionGroup to collect declaratively defined child actions.
/// </summary>
internal interface IActionCollector<TModel> where TModel : class
{
    void AddAction(CrudAction<TModel> action);
}
