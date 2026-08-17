namespace BlazorBase.CRUD.Test.Security.Entities;

/// <summary>
/// Collection-navigation element without its own access restrictions.
/// </summary>
public class SecuredChild
{
    public int Id { get; set; }

    public string? Label { get; set; }
}
