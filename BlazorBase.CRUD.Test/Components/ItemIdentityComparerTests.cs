using BlazorBase.CRUD.Components.Internal;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

public class ItemIdentityComparerTests
{
    private sealed class WithId(Guid id)
    {
        public Guid Id { get; } = id;
    }

    private sealed class WithoutId;

    private sealed class WithNumber(int id)
    {
        public int Id { get; } = id;
    }

    private sealed class WithKeyName(string name)
    {
        [System.ComponentModel.DataAnnotations.Key]
        public string Name { get; } = name;
    }

    private sealed class WithIdAndKey(Guid id, string code)
    {
        public Guid Id { get; } = id;

        [System.ComponentModel.DataAnnotations.Key]
        public string Code { get; } = code;
    }

    private sealed class WithCompositeKey(string first, string second)
    {
        [System.ComponentModel.DataAnnotations.Key]
        public string First { get; } = first;

        [System.ComponentModel.DataAnnotations.Key]
        public string Second { get; } = second;
    }

    [Fact]
    public void ARowKeyedByAKeyProperty_IsTheSameRowAfterItIsReloaded()
    {
        var comparer = ItemIdentityComparer<WithKeyName>.Instance;
        var original = new WithKeyName("nightly");
        var reloaded = new WithKeyName("nightly");

        Assert.True(comparer.Equals(original, reloaded));
        Assert.Equal(comparer.GetHashCode(original), comparer.GetHashCode(reloaded));
        Assert.Contains(reloaded, new HashSet<WithKeyName>([original], comparer));
        Assert.False(comparer.Equals(original, new WithKeyName("weekly")));
    }

    [Fact]
    public void AKeyThatIsNotSet_DoesNotMakeRowsEqual()
    {
        var comparer = ItemIdentityComparer<WithKeyName>.Instance;

        Assert.False(comparer.Equals(new WithKeyName(string.Empty), new WithKeyName(string.Empty)));
        Assert.False(comparer.Equals(new WithKeyName(null!), new WithKeyName(null!)));
    }

    [Fact]
    public void TheIdComesBeforeTheKeyProperty()
    {
        var comparer = ItemIdentityComparer<WithIdAndKey>.Instance;
        var id = Guid.NewGuid();

        Assert.True(comparer.Equals(new WithIdAndKey(id, "a"), new WithIdAndKey(id, "b")));
        Assert.False(comparer.Equals(new WithIdAndKey(Guid.NewGuid(), "a"), new WithIdAndKey(Guid.NewGuid(), "a")));
    }

    [Fact]
    public void ACompositeKey_FallsBackToReference()
    {
        var comparer = ItemIdentityComparer<WithCompositeKey>.Instance;
        var first = new WithCompositeKey("a", "b");

        Assert.True(comparer.Equals(first, first));
        Assert.False(comparer.Equals(first, new WithCompositeKey("a", "b")));
    }

    [Fact]
    public void TwoInstancesWithTheSameId_AreTheSameRow()
    {
        var id = Guid.NewGuid();
        var comparer = ItemIdentityComparer<WithId>.Instance;
        var original = new WithId(id);
        var reloaded = new WithId(id);

        Assert.True(comparer.Equals(original, reloaded));
        Assert.Equal(comparer.GetHashCode(original), comparer.GetHashCode(reloaded));
        Assert.Contains(reloaded, new HashSet<WithId>([original], comparer));
    }

    [Fact]
    public void DifferentIds_AreDifferentRows()
    {
        Assert.False(ItemIdentityComparer<WithId>.Instance.Equals(new WithId(Guid.NewGuid()), new WithId(Guid.NewGuid())));
    }

    [Fact]
    public void ARowWithoutAnId_IsComparedByReference()
    {
        var comparer = ItemIdentityComparer<WithoutId>.Instance;
        var first = new WithoutId();

        Assert.True(comparer.Equals(first, first));
        Assert.False(comparer.Equals(first, new WithoutId()));
    }

    [Fact]
    public void AnIdThatIsNotSetYet_DoesNotMakeNewRowsEqual()
    {
        var comparer = ItemIdentityComparer<WithId>.Instance;

        Assert.False(comparer.Equals(new WithId(Guid.Empty), new WithId(Guid.Empty)));
        Assert.False(ItemIdentityComparer<WithNumber>.Instance.Equals(new WithNumber(0), new WithNumber(0)));
        Assert.True(ItemIdentityComparer<WithNumber>.Instance.Equals(new WithNumber(7), new WithNumber(7)));
    }
}
