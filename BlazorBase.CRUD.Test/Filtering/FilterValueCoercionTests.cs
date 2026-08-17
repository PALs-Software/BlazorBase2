using System.Text.Json;
using BlazorBase.CRUD.Querying;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Filtering;

public class FilterValueCoercionTests
{
    [Fact]
    public void Coerce_Null_ReturnsNull()
        => Assert.Null(FilterValueCoercion.Coerce(null, typeof(int)));

    [Fact]
    public void Coerce_StringToInt()
        => Assert.Equal(5, FilterValueCoercion.Coerce("5", typeof(int)));

    [Fact]
    public void Coerce_JsonNumberToInt()
        => Assert.Equal(5, FilterValueCoercion.Coerce(JsonSerializer.SerializeToElement(5), typeof(int)));

    [Fact]
    public void Coerce_JsonStringToEnum()
        => Assert.Equal(ProductKind.Digital, FilterValueCoercion.Coerce(JsonSerializer.SerializeToElement("Digital"), typeof(ProductKind)));

    [Fact]
    public void Coerce_StringToEnum_CaseInsensitive()
        => Assert.Equal(ProductKind.Service, FilterValueCoercion.Coerce("service", typeof(ProductKind)));

    [Fact]
    public void Coerce_StringToGuid()
    {
        var guid = Guid.NewGuid();
        Assert.Equal(guid, FilterValueCoercion.Coerce(guid.ToString(), typeof(Guid)));
    }

    [Fact]
    public void Coerce_JsonBoolToBool()
        => Assert.Equal(true, FilterValueCoercion.Coerce(JsonSerializer.SerializeToElement(true), typeof(bool)));

    [Fact]
    public void Coerce_StringToBool()
        => Assert.Equal(false, FilterValueCoercion.Coerce("false", typeof(bool)));

    [Fact]
    public void Coerce_StringToDateTime()
        => Assert.Equal(new DateTime(2026, 1, 2), FilterValueCoercion.Coerce("2026-01-02", typeof(DateTime)));

    [Fact]
    public void Coerce_StringToDateOnly()
        => Assert.Equal(new DateOnly(2026, 1, 2), FilterValueCoercion.Coerce("2026-01-02", typeof(DateOnly)));

    [Fact]
    public void Coerce_StringToDecimal_UsesInvariantCulture()
        => Assert.Equal(12.50m, FilterValueCoercion.Coerce("12.50", typeof(decimal)));

    [Fact]
    public void Coerce_NullableTarget_Unwraps()
        => Assert.Equal(7, FilterValueCoercion.Coerce("7", typeof(int?)));
}
