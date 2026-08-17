using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Security.Entities;

/// <summary>
/// Owned-type-style complex reference target with no <c>Id</c> property, used to verify that
/// <see cref="BlazorBase.CRUD.Security.QueryFieldAccessValidator"/> and
/// <see cref="BlazorBase.CRUD.Security.CrudResponseSanitizer"/> still descend into complex
/// reference properties the <see cref="BlazorBase.CRUD.Navigation.NavigationPropertyResolver"/>
/// does not recognize as a navigation.
/// </summary>
public class Compensation
{
    public string? Currency { get; set; }

    [CrudAccess("Admin", "RIM")]
    public decimal Amount { get; set; }
}
