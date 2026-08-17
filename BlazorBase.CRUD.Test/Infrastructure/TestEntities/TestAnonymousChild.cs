namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// A child entity with no display key, no conventional name property and no <c>ToString</c>
/// override - the case whose default rendering used to be the fully qualified CLR type name.
/// </summary>
public class TestAnonymousChild
{
    public Guid Id { get; set; }

    public int Amount { get; set; }
}
