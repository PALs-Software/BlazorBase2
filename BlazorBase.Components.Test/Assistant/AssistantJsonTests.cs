using System.Text.Json;
using BlazorBase.Components.Assistant;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class AssistantJsonTests
{
    public static TheoryData<AssistantStreamEvent> Events() => new()
    {
        new AssistantConversationStartedEvent("c-1"),
        new AssistantTextDeltaEvent("Hello **world**"),
        new AssistantToolStatusEvent("t1", "Searching documentation…", AssistantToolState.Completed),
        new AssistantCitationEvent("Budgets", "https://example.com/b", "snippet"),
        new AssistantPendingActionEvent("a-1", "Set the budget", new Dictionary<string, string> { ["Limit"] = "500" }),
        new AssistantFinishedEvent("m-1", 10, 20),
        new AssistantErrorEvent("rate_limited", "Too many requests", true)
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void EveryEvent_SurvivesARoundTripThroughItsEventName(AssistantStreamEvent original)
    {
        var json = AssistantJson.Serialize(original);

        var restored = AssistantJson.Deserialize(json, AssistantJson.EventName(original));

        Assert.NotNull(restored);
        Assert.IsType(original.GetType(), restored);
        Assert.Equal(AssistantJson.Serialize(original), AssistantJson.Serialize(restored));
    }

    [Fact]
    public void ASerializedEvent_CarriesItsTypeAndUsesCamelCase()
    {
        var json = AssistantJson.Serialize(new AssistantToolStatusEvent("t1", "Working", AssistantToolState.Failed));

        using var document = JsonDocument.Parse(json);
        Assert.Equal("tool_status", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("t1", document.RootElement.GetProperty("toolId").GetString());
        Assert.Equal("failed", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void TheTypeInTheJson_WinsOverTheEventName()
    {
        var restored = AssistantJson.Deserialize("{\"type\":\"text_delta\",\"text\":\"x\"}", "error");

        Assert.Equal(new AssistantTextDeltaEvent("x"), restored);
    }

    [Fact]
    public void WithoutATypeInTheJson_TheEventNameDecides()
    {
        var restored = AssistantJson.Deserialize("{\"text\":\"x\"}", "text_delta");

        Assert.Equal(new AssistantTextDeltaEvent("x"), restored);
    }

    [Fact]
    public void AnUnknownType_IsNullSoNewerServersCanAddEvents()
    {
        Assert.Null(AssistantJson.Deserialize("{\"type\":\"future_event\",\"x\":1}"));
        Assert.Null(AssistantJson.Deserialize("{\"x\":1}"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"type\":\"text_delta\",\"text\":5}")]
    public void InvalidJson_Throws(string json)
    {
        Assert.ThrowsAny<JsonException>(() => AssistantJson.Deserialize(json));
    }
}
