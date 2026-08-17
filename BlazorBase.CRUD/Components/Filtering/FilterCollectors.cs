using BlazorBase.CRUD.Configuration;

namespace BlazorBase.CRUD.Components.Filtering;

/// <summary>
/// Implemented by <see cref="BaseList{TModel}"/> to collect a declarative <see cref="FilterConfig{TModel}"/> child.
/// </summary>
internal interface IFilterConfigCollector<TModel> where TModel : class
{
    void RegisterFilterConfig(FilterConfig<TModel> filterConfig);
}

/// <summary>
/// Implemented by <see cref="FilterConfig{TModel}"/> to collect its declarative <see cref="FilterField{TModel}"/> children.
/// </summary>
internal interface IFilterFieldCollector<TModel> where TModel : class
{
    void AddField(FilterFieldConfig<TModel> field);
}
