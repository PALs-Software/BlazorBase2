using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorBase.Components.Assistant;

/// <summary>The JSON settings of the assistant wire format, for the client and for the server that produces the stream.</summary>
public static class AssistantJson
{
    private static readonly IReadOnlyDictionary<string, Type> EventTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [AssistantStreamEventTypes.ConversationStarted] = typeof(AssistantConversationStartedEvent),
        [AssistantStreamEventTypes.TextDelta] = typeof(AssistantTextDeltaEvent),
        [AssistantStreamEventTypes.ToolStatus] = typeof(AssistantToolStatusEvent),
        [AssistantStreamEventTypes.Citation] = typeof(AssistantCitationEvent),
        [AssistantStreamEventTypes.PendingAction] = typeof(AssistantPendingActionEvent),
        [AssistantStreamEventTypes.Finished] = typeof(AssistantFinishedEvent),
        [AssistantStreamEventTypes.Error] = typeof(AssistantErrorEvent)
    };

    /// <summary>camelCase names, enums as camelCase strings, null properties omitted.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Serializes <paramref name="streamEvent"/> including its <c>type</c> discriminator.</summary>
    public static string Serialize(AssistantStreamEvent streamEvent)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);
        return JsonSerializer.Serialize(streamEvent, Options);
    }

    /// <summary>The SSE event name of <paramref name="streamEvent"/>.</summary>
    public static string EventName(AssistantStreamEvent streamEvent)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);

        foreach (var (name, type) in EventTypes)
            if (type == streamEvent.GetType())
                return name;

        throw new ArgumentException($"Unknown event type {streamEvent.GetType().Name}.", nameof(streamEvent));
    }

    /// <summary>
    /// Reads one event. The type comes from the <c>type</c> property of the JSON, or from <paramref name="eventName"/>
    /// when the JSON carries none.
    /// </summary>
    /// <returns>The event, or null when the type is unknown to this version (so newer servers can add event types).</returns>
    /// <exception cref="JsonException">The JSON is invalid or does not fit the event type.</exception>
    public static AssistantStreamEvent? Deserialize(string json, string? eventName = null)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("An assistant event must be a JSON object.");

        var typeName = document.RootElement.TryGetProperty("type", out var typeProperty) && typeProperty.ValueKind == JsonValueKind.String
            ? typeProperty.GetString()
            : eventName;

        if (typeName is null || !EventTypes.TryGetValue(typeName, out var type))
            return null;

        return (AssistantStreamEvent?)document.RootElement.Deserialize(type, Options);
    }
}
