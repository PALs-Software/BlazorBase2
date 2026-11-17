namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Entity for <see cref="BaseEndpointMapperNotFoundTests"/>. Deliberately has no
/// <see cref="Attributes.BaseCrudAttribute"/> and no <see cref="Attributes.CrudAccessAttribute"/>, so the
/// assembly-scanning tests never map it and every authenticated principal passes the class-level gate.
/// <see cref="Tenant"/> is what <see cref="HiddenRowTestDbContext"/> filters on.
/// </summary>
public class HiddenRowTestEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Tenant { get; set; } = string.Empty;
}
