using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// Catches the exceptions of a card's content and shows them inside the card, so that a failed action leaves the card
/// open with the entered data instead of replacing the whole application with an error page.
/// </summary>
/// <remarks>
/// Unlike <see cref="ErrorBoundary"/> the content is always rendered: replacing it would throw away the fields the
/// user is working on, and the card builds its field list from the children that register themselves while rendering.
/// </remarks>
public sealed class CardErrorBoundary : ErrorBoundaryBase
{
    [Inject]
    private ILogger<CardErrorBoundary> Logger { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "An unhandled exception occurred inside a card.");
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (CurrentException is { } exception && ErrorContent is not null)
            builder.AddContent(0, ErrorContent, exception);

        builder.AddContent(1, ChildContent);
    }
}
