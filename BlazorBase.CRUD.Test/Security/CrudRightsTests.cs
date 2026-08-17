using BlazorBase.CRUD.Security;
using Xunit;

namespace BlazorBase.CRUD.Test.Security;

public class CrudRightsTests
{
    [Fact]
    public void None_IsZero()
    {
        Assert.Equal(0, (int)CrudRights.None);
    }

    [Fact]
    public void All_IsUnionOfEveryRight()
    {
        Assert.Equal(CrudRights.Read | CrudRights.Insert | CrudRights.Modify | CrudRights.Delete, CrudRights.All);
    }

    [Fact]
    public void All_ContainsEveryIndividualFlag()
    {
        Assert.True(CrudRights.All.HasFlag(CrudRights.Read));
        Assert.True(CrudRights.All.HasFlag(CrudRights.Insert));
        Assert.True(CrudRights.All.HasFlag(CrudRights.Modify));
        Assert.True(CrudRights.All.HasFlag(CrudRights.Delete));
    }

    [Fact]
    public void Intersection_NarrowsToCommonFlags()
    {
        var result = (CrudRights.Read | CrudRights.Modify) & CrudRights.Read;

        Assert.Equal(CrudRights.Read, result);
    }
}
