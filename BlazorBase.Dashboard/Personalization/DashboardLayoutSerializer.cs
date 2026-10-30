using System.Text.Json;

namespace BlazorBase.Dashboard;

/// <summary>
/// Converts a <see cref="DashboardLayout"/> to and from the JSON that the browser storage and the server table keep. Reading is
/// forgiving: a missing or damaged value is an empty layout, never an exception, so a bad row cannot break the dashboard.
/// </summary>
public static class DashboardLayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes the parts of a layout.
    /// </summary>
    public static string Serialize(DashboardLayout layout) => JsonSerializer.Serialize(layout.Parts ?? [], Options);

    /// <summary>
    /// Reads a layout; null, empty or damaged input yields an empty layout.
    /// </summary>
    public static DashboardLayout Deserialize(string? json)
    {
        if (String.IsNullOrWhiteSpace(json))
            return new DashboardLayout();

        try
        {
            var parts = JsonSerializer.Deserialize<List<DashboardPartPreference>>(json, Options);
            return new DashboardLayout { Parts = [.. (parts ?? []).Where(part => part is not null && !String.IsNullOrEmpty(part.PartId))] };
        }
        catch (JsonException)
        {
            return new DashboardLayout();
        }
    }
}
