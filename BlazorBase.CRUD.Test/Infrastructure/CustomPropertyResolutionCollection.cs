using Xunit;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// Groups the component tests that read or populate the process-wide custom-property resolution
/// caches into a single non-parallel xUnit collection. The caches are static and shared across the
/// process; running these tests serially prevents one test's DI registration set from leaking into
/// another's cached resolution decision.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CustomPropertyResolutionCollection
{
    public const string Name = "CustomPropertyResolution";
}
