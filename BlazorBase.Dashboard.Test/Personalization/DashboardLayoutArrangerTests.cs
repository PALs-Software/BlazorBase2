using Xunit;

namespace BlazorBase.Dashboard.Test.Personalization;

public sealed class DashboardLayoutArrangerTests
{
    private static DashboardPartRegistration Part(string id) => new() { Id = id, ComponentType = typeof(Infrastructure.PartA) };

    private static List<DashboardPartRegistration> Parts(params string[] ids) => [.. ids.Select(Part)];

    private static DashboardLayout Layout(params (string Id, bool Hidden)[] preferences) => new()
    {
        Parts = [.. preferences.Select((item, index) => new DashboardPartPreference { PartId = item.Id, Hidden = item.Hidden, Order = index })]
    };

    private static string[] Ids(IEnumerable<DashboardArrangedPart> arranged) => [.. arranged.Select(part => part.Registration.Id)];

    [Fact]
    public void NoLayout_KeepsTheDefaultOrder_AndHidesNothing()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "b", "c"), null);

        Assert.Equal(["a", "b", "c"], Ids(arranged));
        Assert.DoesNotContain(arranged, part => part.Hidden);
    }

    [Fact]
    public void Order_OverridesThePriorityOrder()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "b", "c"), Layout(("c", false), ("a", false), ("b", false)));

        Assert.Equal(["c", "a", "b"], Ids(arranged));
    }

    [Fact]
    public void HiddenFlag_IsCarriedToTheArrangedPart()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "b"), Layout(("a", false), ("b", true)));

        Assert.False(arranged[0].Hidden);
        Assert.True(arranged[1].Hidden);
    }

    [Fact]
    public void UnknownIdsInTheLayout_AreIgnored_AndNeverAddAParts()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "b"), Layout(("gone", false), ("b", false), ("admin-only", true), ("a", false)));

        Assert.Equal(["b", "a"], Ids(arranged));
    }

    [Fact]
    public void PartsTheLayoutDoesNotKnow_UseTheirDefaultPosition_AfterTheirDefaultPredecessor()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "new", "b", "c"), Layout(("c", false), ("b", false), ("a", false)));

        Assert.Equal(["c", "b", "a", "new"], Ids(arranged));
    }

    [Fact]
    public void ANewFirstPart_IsPlacedAtTheStart()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("new", "a", "b"), Layout(("b", false), ("a", false)));

        Assert.Equal(["new", "b", "a"], Ids(arranged));
    }

    [Fact]
    public void SeveralNewParts_KeepTheirDefaultRelativeOrder()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "n1", "n2", "b"), Layout(("b", false), ("a", false)));

        Assert.Equal(["b", "a", "n1", "n2"], Ids(arranged));
    }

    [Fact]
    public void DuplicateIdsInTheLayout_UseTheFirstOne()
    {
        var layout = new DashboardLayout
        {
            Parts =
            [
                new DashboardPartPreference { PartId = "a", Hidden = true, Order = 0 },
                new DashboardPartPreference { PartId = "a", Hidden = false, Order = 1 }
            ]
        };

        var arranged = DashboardLayoutArranger.Arrange(Parts("a"), layout);

        Assert.True(Assert.Single(arranged).Hidden);
    }

    [Fact]
    public void ToLayout_NumbersThePartsFromZero_InTheGivenOrder()
    {
        var arranged = DashboardLayoutArranger.Arrange(Parts("a", "b"), Layout(("b", true), ("a", false)));

        var layout = DashboardLayoutArranger.ToLayout(arranged);

        Assert.Equal(["b", "a"], layout.Parts.Select(part => part.PartId));
        Assert.Equal([0, 1], layout.Parts.Select(part => part.Order));
        Assert.Equal([true, false], layout.Parts.Select(part => part.Hidden));
    }

    [Fact]
    public void EmptyLayoutInput_IsTheDefaultLayout()
    {
        Assert.True(new DashboardLayout().IsDefault);
        Assert.False(Layout(("a", false)).IsDefault);
    }
}
