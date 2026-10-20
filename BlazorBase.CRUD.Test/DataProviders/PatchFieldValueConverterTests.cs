using System.Text.Json;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

public class PatchFieldValueConverterTests
{
    [Fact]
    public void ConvertToClrTypes_StringField_DeserializesToString()
    {
        var changedFields = new Dictionary<string, object?> { ["Title"] = JsonSerializer.SerializeToElement("padded") };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<string>(converted["Title"]);
        Assert.Equal("padded", converted["Title"]);
    }

    [Fact]
    public void ConvertToClrTypes_BoolField_DeserializesToBool()
    {
        var changedFields = new Dictionary<string, object?> { ["IsFlagged"] = JsonSerializer.SerializeToElement(true) };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<bool>(converted["IsFlagged"]);
        Assert.Equal(true, converted["IsFlagged"]);
    }

    [Fact]
    public void ConvertToClrTypes_NullableEnumField_DeserializesToEnum()
    {
        var changedFields = new Dictionary<string, object?> { ["Kind"] = JsonSerializer.SerializeToElement(ProductKind.Service) };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<ProductKind>(converted["Kind"]);
        Assert.Equal(ProductKind.Service, converted["Kind"]);
    }

    [Fact]
    public void ConvertToClrTypes_NullableGuidField_DeserializesToGuid()
    {
        var guid = Guid.NewGuid();
        var changedFields = new Dictionary<string, object?> { ["ReferenceId"] = JsonSerializer.SerializeToElement(guid) };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<Guid>(converted["ReferenceId"]);
        Assert.Equal(guid, converted["ReferenceId"]);
    }

    [Fact]
    public void ConvertToClrTypes_NullableDecimalField_DeserializesToDecimal()
    {
        var changedFields = new Dictionary<string, object?> { ["Amount"] = JsonSerializer.SerializeToElement(19.99m) };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<decimal>(converted["Amount"]);
        Assert.Equal(19.99m, converted["Amount"]);
    }

    [Fact]
    public void ConvertToClrTypes_NullableDateTimeField_DeserializesToDateTime()
    {
        var when = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var changedFields = new Dictionary<string, object?> { ["OccurredOn"] = JsonSerializer.SerializeToElement(when) };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<DateTime>(converted["OccurredOn"]);
        Assert.Equal(when, converted["OccurredOn"]);
    }

    [Fact]
    public void ConvertToClrTypes_JsonNull_ConvertsToClrNull()
    {
        var changedFields = new Dictionary<string, object?> { ["Amount"] = JsonSerializer.SerializeToElement<decimal?>(null) };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.Null(converted["Amount"]);
    }

    [Fact]
    public void ConvertToClrTypes_ClrNull_StaysNull()
    {
        var changedFields = new Dictionary<string, object?> { ["Amount"] = null };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.Null(converted["Amount"]);
    }

    [Fact]
    public void ConvertToClrTypes_UnknownFieldName_PassesValueThroughUnconverted()
    {
        var jsonValue = JsonSerializer.SerializeToElement("whatever");
        var changedFields = new Dictionary<string, object?> { ["NoSuchField"] = jsonValue };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.IsType<JsonElement>(converted["NoSuchField"]);
        Assert.Equal(jsonValue.GetRawText(), ((JsonElement)converted["NoSuchField"]!).GetRawText());
    }

    [Fact]
    public void ConvertToClrTypes_AlreadyTypedValue_PassesThroughUnchanged()
    {
        var changedFields = new Dictionary<string, object?> { ["Title"] = "already typed" };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestPatchTarget), changedFields);

        Assert.Equal("already typed", converted["Title"]);
    }

    [Fact]
    public void ConvertToClrTypes_ExcludedProperty_PassesValueThroughUnconverted()
    {
        var jsonValue = JsonSerializer.SerializeToElement("hacked");
        var changedFields = new Dictionary<string, object?> { ["InternalNotes"] = jsonValue };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestProduct), changedFields);

        Assert.IsType<JsonElement>(converted["InternalNotes"]);
    }

    [Fact]
    public void ConvertToClrTypes_NavigationProperty_PassesValueThroughUnconverted()
    {
        var jsonValue = JsonSerializer.SerializeToElement(new { Id = Guid.NewGuid(), Name = "x" });
        var changedFields = new Dictionary<string, object?> { ["Category"] = jsonValue };

        var converted = PatchFieldValueConverter.ConvertToClrTypes(typeof(TestProduct), changedFields);

        Assert.IsType<JsonElement>(converted["Category"]);
    }

    [Fact]
    public void ConvertValue_JsonElementNull_ReturnsNull()
    {
        var jsonNull = JsonSerializer.SerializeToElement<string?>(null);

        var result = PatchFieldValueConverter.ConvertValue(jsonNull, typeof(string));

        Assert.Null(result);
    }

    [Fact]
    public void ConvertValue_AlreadyClrValue_ReturnsSameValue()
    {
        var result = PatchFieldValueConverter.ConvertValue(42, typeof(int));

        Assert.Equal(42, result);
    }
}
