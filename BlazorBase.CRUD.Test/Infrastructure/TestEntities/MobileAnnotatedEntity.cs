using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Configuration;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// Entity that declares its tile on the properties with <see cref="MobileVisibleAttribute"/>.
/// </summary>
public class MobileAnnotatedEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    [MobileVisible(Role = MobileRole.Title)]
    public string Name { get; set; } = string.Empty;

    [MobileVisible(Order = 2)]
    public DateTime CreatedOn { get; set; }

    [MobileVisible(Order = 1)]
    public decimal Amount { get; set; }

    [MobileVisible(Role = MobileRole.Badge)]
    public bool IsActive { get; set; }

    [MobileVisible(Role = MobileRole.Hidden)]
    public string Secret { get; set; } = string.Empty;
}
