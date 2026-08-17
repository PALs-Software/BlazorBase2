using BlazorBase.CRUD.Security;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Security.Entities;
using Xunit;

namespace BlazorBase.CRUD.Test.Security;

/// <summary>
/// The query endpoint hands the sanitizer a <c>List&lt;TModel&gt;</c>. That binds to the single-item
/// overload with <c>TModel = List&lt;TModel&gt;</c> — an identity conversion beats the reference
/// conversion the collection overload would need — so the sanitizer used to treat the list itself as
/// the entity: a <c>500</c> for a <c>List</c> (whose <c>Item</c> indexer cannot be read without an
/// index) and, worse, silently unsanitized elements for an array. These tests pin every shape a
/// caller can pass.
/// </summary>
public class CrudResponseSanitizerCollectionTests
{
    [Fact]
    public void Strip_StripsEveryElementOfAList()
    {
        var entities = new List<SecuredEntity> { CreateEntity(), CreateEntity() };

        CrudResponseSanitizer.Strip(entities, TestPrincipals.Anonymous());

        Assert.All(entities, entity =>
        {
            Assert.Equal(0m, entity.Salary);
            Assert.Null(entity.Secret);
            Assert.Equal("Alice", entity.Name);
        });
    }

    [Fact]
    public void Strip_StripsEveryElementOfAnArray()
    {
        var entities = new[] { CreateEntity(), CreateEntity() };

        CrudResponseSanitizer.Strip(entities, TestPrincipals.Anonymous());

        Assert.All(entities, entity =>
        {
            Assert.Equal(0m, entity.Salary);
            Assert.Null(entity.Secret);
        });
    }

    [Fact]
    public void Strip_StripsElements_WhenTheSequenceIsTypedAsTheInterface()
    {
        IEnumerable<SecuredEntity> entities = new List<SecuredEntity> { CreateEntity() };

        CrudResponseSanitizer.Strip(entities, TestPrincipals.Anonymous());

        Assert.Equal(0m, entities.First().Salary);
    }

    [Fact]
    public void Strip_LeavesEverythingInPlaceForAnAdmin_WhicheverShapeArrives()
    {
        var list = new List<SecuredEntity> { CreateEntity() };
        var array = new[] { CreateEntity() };

        CrudResponseSanitizer.Strip(list, TestPrincipals.WithRoles("Admin"));
        CrudResponseSanitizer.Strip(array, TestPrincipals.WithRoles("Admin"));

        Assert.Equal(5000m, list[0].Salary);
        Assert.NotNull(list[0].Secret);
        Assert.Equal(5000m, array[0].Salary);
        Assert.NotNull(array[0].Secret);
    }

    [Fact]
    public void Strip_IgnoresNullElements()
    {
        var entities = new List<SecuredEntity?> { null, CreateEntity() };

        CrudResponseSanitizer.Strip(entities, TestPrincipals.Anonymous());

        Assert.Equal(0m, entities[1]!.Salary);
    }

    [Fact]
    public void Strip_TreatsAStringAsAValueRatherThanASequenceOfCharacters()
    {
        CrudResponseSanitizer.Strip("a plain string", TestPrincipals.Anonymous());
    }

    [Fact]
    public void Strip_HandlesAnEmptySequence()
    {
        CrudResponseSanitizer.Strip(new List<SecuredEntity>(), TestPrincipals.Anonymous());
    }

    private static SecuredEntity CreateEntity() => new()
    {
        Id = 1,
        Name = "Alice",
        Salary = 5000m,
        Secret = new SecretDetail { Id = 1, Value = "TopSecret" },
        Public = new PublicDetail { Info = "General info", RestrictedInfo = "Restricted info" }
    };
}
