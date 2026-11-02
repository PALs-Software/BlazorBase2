using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Keys;

public class EntityKeyResolverTests
{
    [Fact]
    public void AnIdProperty_IsTheKey()
        => Assert.Equal("Id", EntityKeyResolver.Find(typeof(TestStringKeyedNote))?.Name);

    [Fact]
    public void TheTypeNameWithIdSuffix_IsTheKeyWithoutAnAttribute()
        => Assert.Equal(nameof(TestTypeNameKeyed.TestTypeNameKeyedId), EntityKeyResolver.Find(typeof(TestTypeNameKeyed))?.Name);

    [Fact]
    public void TheKeyAttribute_IsTheKeyOnAnyProperty()
        => Assert.Equal(nameof(TestAttributeKeyed.Code), EntityKeyResolver.Find(typeof(TestAttributeKeyed))?.Name);

    [Fact]
    public void AnIdProperty_KeepsPrecedenceOverTheKeyAttribute()
        => Assert.Equal(nameof(TestAttributeKeyedWithDecoyId.Id), EntityKeyResolver.Find(typeof(TestAttributeKeyedWithDecoyId))?.Name);

    [Fact]
    public void ACompositeKey_ResolvesToNothing()
        => Assert.Null(EntityKeyResolver.Find(typeof(TestCompositeKeyed)));

    [Fact]
    public void ATypeWithoutAKey_ResolvesToNothingAndNamesIdByDefault()
    {
        Assert.Null(EntityKeyResolver.Find(typeof(TestKeyless)));
        Assert.Equal(EntityKeyResolver.DefaultKeyName, EntityKeyResolver.GetName(typeof(TestKeyless)));
    }

    [Fact]
    public void GetName_NamesTheResolvedKey()
        => Assert.Equal(nameof(TestTypeNameKeyed.TestTypeNameKeyedId), EntityKeyResolver.GetName(typeof(TestTypeNameKeyed)));

    [Fact]
    public void GetValue_ReadsTheResolvedKey()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, EntityKeyResolver.GetValue(new TestTypeNameKeyed { TestTypeNameKeyedId = id }));
        Assert.Null(EntityKeyResolver.GetValue(new TestKeyless()));
    }
}
