using Xunit;

namespace BlazorBase.CRUD.Test.Security;

/// <summary>
/// Groups every test that reads or mutates <see cref="CrudAccessResolver.StrictMode"/> — a
/// process-wide static switch — so xUnit never runs them in parallel with each other. Without
/// this, a test flipping the flag could race with <see cref="CrudAccessResolverTests"/>'s
/// unannotated-entity assertions, which assume the (default, off) permissive behavior.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CrudAccessResolverCollection
{
    public const string Name = "CrudAccessResolver.StrictMode (process-global static state)";
}
