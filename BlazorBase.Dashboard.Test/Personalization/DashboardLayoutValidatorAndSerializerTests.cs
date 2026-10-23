using Xunit;

namespace BlazorBase.Dashboard.Test.Personalization;

public sealed class DashboardLayoutValidatorAndSerializerTests
{
    private static DashboardLayout Layout(params string[] ids) => new()
    {
        Parts = [.. ids.Select((id, index) => new DashboardPartPreference { PartId = id, Order = index })]
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyKey_IsRejected(string? key) =>
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateKey(key));

    [Fact]
    public void OverlongKey_IsRejected_AndTheLimitItselfIsAccepted()
    {
        Assert.Equal(DashboardLayoutValidator.MaxKeyLength, DashboardLayoutValidator.ValidateKey(new string('k', DashboardLayoutValidator.MaxKeyLength)).Length);
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateKey(new string('k', DashboardLayoutValidator.MaxKeyLength + 1)));
    }

    [Fact]
    public void MoreThanSixtyFourParts_AreRejected_ButSixtyFourAreAccepted()
    {
        DashboardLayoutValidator.ValidateLayout(Layout([.. Enumerable.Range(0, 64).Select(index => $"p{index}")]));

        Assert.Throws<DashboardLayoutValidationException>(() =>
            DashboardLayoutValidator.ValidateLayout(Layout([.. Enumerable.Range(0, 65).Select(index => $"p{index}")])));
    }

    [Fact]
    public void EmptyOverlongOrDuplicatePartIds_AreRejected()
    {
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateLayout(Layout("a", " ")));
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateLayout(Layout(new string('p', DashboardLayoutValidator.MaxPartIdLength + 1))));
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateLayout(Layout("a", "a")));
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateLayout(null));
    }

    [Fact]
    public void OrderOutsideTheRange_IsRejected()
    {
        var layout = new DashboardLayout { Parts = [new DashboardPartPreference { PartId = "a", Order = -1 }] };
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateLayout(layout));

        layout.Parts[0].Order = DashboardLayoutValidator.MaxOrder + 1;
        Assert.Throws<DashboardLayoutValidationException>(() => DashboardLayoutValidator.ValidateLayout(layout));
    }

    [Fact]
    public void Normalize_SortsByOrder_AndCopies()
    {
        var input = new DashboardLayout
        {
            Parts =
            [
                new DashboardPartPreference { PartId = "b", Order = 5 },
                new DashboardPartPreference { PartId = "a", Order = 1, Hidden = true }
            ]
        };

        var normalized = DashboardLayoutValidator.Normalize(input);

        Assert.Equal(["a", "b"], normalized.Parts.Select(part => part.PartId));
        Assert.True(normalized.Parts[0].Hidden);
        Assert.NotSame(input.Parts[0], normalized.Parts[1]);
    }

    [Fact]
    public void Serializer_RoundTrips()
    {
        var layout = new DashboardLayout { Parts = [new DashboardPartPreference { PartId = "a", Hidden = true, Order = 3 }] };

        var read = DashboardLayoutSerializer.Deserialize(DashboardLayoutSerializer.Serialize(layout));

        var part = Assert.Single(read.Parts);
        Assert.Equal(("a", true, 3), (part.PartId, part.Hidden, part.Order));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"parts\": 1}")]
    public void Serializer_TurnsMissingOrDamagedInputIntoTheDefaultLayout(string? json) =>
        Assert.True(DashboardLayoutSerializer.Deserialize(json).IsDefault);

    [Fact]
    public void Serializer_DropsEntriesWithoutAnId()
    {
        var read = DashboardLayoutSerializer.Deserialize("[{\"partId\":\"a\"},{\"partId\":\"\"},null]");

        Assert.Equal(["a"], read.Parts.Select(part => part.PartId));
    }
}
