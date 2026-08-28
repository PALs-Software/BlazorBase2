using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Minimal entity for <see cref="BaseEndpointMapperConfiguratorTests"/> — deliberately carries no
/// <see cref="CrudAccessAttribute"/> so every authenticated principal passes the class-level gate
/// and the tests exercise only <see cref="Endpoints.BaseEndpointOptions{TEntity}.UserFilter"/>.
/// </summary>
[BaseCrud("configurator-entities")]
public class ConfiguratorTestEntity
{
    public int Id { get; set; }

    public string Owner { get; set; } = string.Empty;
}
