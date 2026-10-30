using BlazorBase.Components.Routing;
using BlazorBase.CRUD.Components.Internal;
using Microsoft.AspNetCore.Components.Routing;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Keeps a deep-linked card with unsaved changes from being discarded silently by a navigation: the browser's back
/// and forward buttons, the back gesture of a phone and links of the page all ask the same question the close
/// button asks.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    private IDisposable? LocationGuard { get; set; }
    private CardCloseGuard? OpenItemGuard { get; set; }
    private string? PendingNavigationTarget { get; set; }

    /// <summary>
    /// Starts watching navigations while the deep-linked card of <paramref name="guard"/> is open.
    /// </summary>
    /// <remarks>
    /// A host whose navigation manager cannot lock navigation falls back to the former behavior: the card closes
    /// without asking when the URL moves on.
    /// </remarks>
    private void ArmNavigationGuard(CardCloseGuard guard)
    {
        DisarmNavigationGuard();
        OpenItemGuard = guard;

        try
        {
            LocationGuard = Navigation.RegisterLocationChangingHandler(OnLocationChangingAsync);
        }
        catch (NotSupportedException)
        {
            LocationGuard = null;
        }
    }

    private void DisarmNavigationGuard()
    {
        LocationGuard?.Dispose();
        LocationGuard = null;
        OpenItemGuard = null;
    }

    /// <summary>
    /// Holds a navigation that would leave the open card while it has unsaved changes and asks the card to close. The
    /// navigation is carried out once the user chose to discard; keeping the card open drops it.
    /// </summary>
    private async ValueTask OnLocationChangingAsync(LocationChangingContext context)
    {
        var guard = OpenItemGuard;

        if (IsDisposed || guard is null || CurrentOpenKey is null || !guard.HasUnsavedChanges)
            return;

        if (!LeavesOpenItem(Navigation.Uri, CurrentOpenKey, context.TargetLocation, DeepLinkParameterName))
            return;

        context.PreventNavigation();

        if (PendingNavigationTarget is not null)
            return;

        PendingNavigationTarget = context.TargetLocation;

        if (!await guard.RequestCloseAsync())
            PendingNavigationTarget = null;
    }

    /// <summary>
    /// Carries on after the card closed: to the location a held navigation wanted, or, when the card closed by
    /// itself, to the list without the deep link parameter.
    /// </summary>
    private void CompleteClosedItemNavigation()
    {
        var target = PendingNavigationTarget;
        PendingNavigationTarget = null;

        if (target is null)
            StripDeepLinkParameter();
        else
            Navigation.NavigateTo(target, replace: true);
    }

    /// <summary>
    /// Whether going to <paramref name="targetLocation"/> from <paramref name="currentUri"/> leaves the card that is
    /// open for <paramref name="openKey"/>: another page, another item or no item at all.
    /// </summary>
    internal static bool LeavesOpenItem(string currentUri, string openKey, string targetLocation, string parameterName)
    {
        if (!Uri.TryCreate(currentUri, UriKind.Absolute, out var current)
            || !Uri.TryCreate(current, targetLocation, out var target))
            return true;

        if (!string.Equals(current.AbsolutePath, target.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            return true;

        var targetKey = QueryStringReader.TryReadValue(target.AbsoluteUri, parameterName);

        return !string.Equals(string.IsNullOrEmpty(targetKey) ? null : targetKey, openKey, StringComparison.Ordinal);
    }
}
