using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Configuration;

public class ExpressionHelperTests
{
    [Fact]
    public void GetPropertyName_ReturnsSimplePropertyName()
    {
        var name = ExpressionHelper.GetPropertyName<TestProduct>(p => p.Name);

        Assert.Equal("Name", name);
    }

    [Fact]
    public void GetPropertyName_UnwrapsBoxedValueType()
    {
        var name = ExpressionHelper.GetPropertyName<TestProduct>(p => p.Stock);

        Assert.Equal("Stock", name);
    }

    [Fact]
    public void GetPropertyName_ReturnsDottedPathForNestedMember()
    {
        var name = ExpressionHelper.GetPropertyName<TestProduct>(p => p.Category!.Name);

        Assert.Equal("Category.Name", name);
    }

    [Fact]
    public void GetPropertyInfo_ReturnsBackingProperty()
    {
        var property = ExpressionHelper.GetPropertyInfo<TestProduct>(p => p.Price);

        Assert.Equal("Price", property.Name);
        Assert.Equal(typeof(decimal), property.PropertyType);
    }

    [Fact]
    public void GetPropertyName_ThrowsForNonPropertyExpression()
    {
        Assert.Throws<ArgumentException>(
            () => ExpressionHelper.GetPropertyName<TestProduct>(p => p.GetHashCode()));
    }
}
