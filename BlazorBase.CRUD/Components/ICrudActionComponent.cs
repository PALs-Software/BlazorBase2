using BlazorBase.CRUD.Events;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Implement this interface on components that are rendered dynamically by a CRUD action.
/// The component receives the action context and signals closure via OnClose.
/// </summary>
public interface ICrudActionComponent<TModel> where TModel : class
{
    CrudActionEventArgs<TModel> Args { get; set; }

    EventCallback OnClose { get; set; }
}
