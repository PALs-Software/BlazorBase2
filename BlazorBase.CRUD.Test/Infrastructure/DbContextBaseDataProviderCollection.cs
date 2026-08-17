using Xunit;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// Groups the <see cref="DataProviders.DbContextBaseDataProviderTests"/> page-size clamp tests into
/// a single non-parallel xUnit collection. Those tests mutate the process-wide static
/// <see cref="BlazorBase.CRUD.Models.BaseQueryLimits.MaxPageSize"/>; running them serially prevents
/// any other test exercising the real provider from observing a mutated value mid-run.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DbContextBaseDataProviderCollection
{
    public const string Name = "DbContextBaseDataProvider";
}
