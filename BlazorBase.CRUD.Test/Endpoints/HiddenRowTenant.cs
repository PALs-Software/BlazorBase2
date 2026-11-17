namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// The tenant a <see cref="HiddenRowTestDbContext"/> instance may see; every row of another tenant is
/// hidden by the context's global query filter.
/// </summary>
public sealed record HiddenRowTenant(string Name);
