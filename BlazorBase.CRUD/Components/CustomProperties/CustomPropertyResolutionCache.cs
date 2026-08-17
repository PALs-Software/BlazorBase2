using System.Collections.Concurrent;

namespace BlazorBase.CRUD.Components.CustomProperties;

/// <summary>
/// Process-wide caches that map a property shape to the resolved custom component type (or a "none"
/// sentinel), so the registered-component iteration runs once per field shape instead of per render.
/// Shared across all closed generic instantiations of the card/list components because the key
/// already carries the model type.
/// </summary>
internal static class CustomPropertyResolutionCache
{
    /// <summary>Card-input decisions keyed by (model type, property metadata token, edit mode).</summary>
    internal static readonly ConcurrentDictionary<(Type ModelType, int MetadataToken, bool IsEditing), Type?> Inputs = new();

    /// <summary>List-cell display decisions keyed by (model type, property metadata token).</summary>
    internal static readonly ConcurrentDictionary<(Type ModelType, int MetadataToken), Type?> Displays = new();

    internal static void ResetForTests()
    {
        Inputs.Clear();
        Displays.Clear();
    }
}
