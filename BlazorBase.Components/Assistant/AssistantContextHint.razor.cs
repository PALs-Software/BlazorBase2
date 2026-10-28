using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.Components.Assistant;

/// <summary>
/// Tells the assistant something about the part of the page it is placed on, for as long as it is rendered. It
/// renders only its <see cref="ChildContent"/> and does nothing when the assistant is not registered.
/// </summary>
/// <remarks>
/// Pass ids and technical values, never field values. Several hints on one page add up; for the single-valued
/// parameters the hint rendered last wins.
/// </remarks>
public sealed partial class AssistantContextHint(IServiceProvider serviceProvider) : ComponentBase, IAssistantContextProvider, IDisposable
{
    #region Injects

    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    /// <summary>The name of a free hint; used together with <see cref="Value"/>.</summary>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>The value of the free hint named <see cref="Key"/>.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>The type of the entity the part shows.</summary>
    [Parameter]
    public string? EntityType { get; set; }

    /// <summary>The key of the selected or open record.</summary>
    [Parameter]
    public string? EntityKey { get; set; }

    /// <summary>A short description of the active filters.</summary>
    [Parameter]
    public string? Filters { get; set; }

    /// <summary>The id of the active list preset.</summary>
    [Parameter]
    public string? Preset { get; set; }

    /// <summary>The error text visible in this part.</summary>
    [Parameter]
    public string? ErrorText { get; set; }

    /// <summary>Content rendered unchanged.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    #region Members

    private IDisposable? Registration;

    #endregion

    /// <inheritdoc/>
    protected override void OnInitialized() =>
        Registration = ServiceProvider.GetService<AssistantContextService>()?.Register(this);

    /// <inheritdoc/>
    public void Contribute(AssistantContextBuilder builder)
    {
        builder.AddHint(Key ?? string.Empty, Value);
        builder.EntityType = EntityType ?? builder.EntityType;
        builder.EntityKey = EntityKey ?? builder.EntityKey;
        builder.Filters = Filters ?? builder.Filters;
        builder.Preset = Preset ?? builder.Preset;
        builder.ErrorText = ErrorText ?? builder.ErrorText;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Registration?.Dispose();
        Registration = null;
    }
}
