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
