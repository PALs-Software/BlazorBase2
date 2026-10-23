using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.Dashboard;

/// <summary>
/// Personalization of the role center: the edit mode in which a user hides, shows and reorders the parts they may see.
/// </summary>
/// <remarks>
/// The edit mode exists only when an <see cref="IDashboardLayoutStore"/> is registered. The saved layout is read before the first part
/// is rendered, so hidden parts are neither rendered nor fetched. Role rules always run first; a layout only reorders and hides
/// what the user may see anyway. When the store cannot be reached the default layout is used and the dashboard keeps working.
/// </remarks>
public partial class RoleCenter
{
    #region Parameters

    /// <summary>
    /// The key the layout of this role center is stored under. Defaults to <see cref="DashboardLayout.DefaultKey"/>, the route of
    /// the start page; set it when a host has more than one role center.
    /// </summary>
    [Parameter]
    public string? LayoutKey { get; set; }

    #endregion

    private IDashboardLayoutStore? Store;

    private DashboardLayout Layout = new();

    private List<DashboardArrangedPart> Arranged = [];

    private bool IsEditing;

    private bool IsSaving;

    private bool IsConfirmingReset;

    private string? EditError;

    private string Announcement = String.Empty;

    private bool AnnouncementFlip;

    private (string PartId, string Action)? PendingControlFocus;

    private string? PendingFocusId;

    private readonly string InstanceId = Guid.NewGuid().ToString("N")[..8];

    private bool IsPersonalizable => Store is not null;

    private string EffectiveLayoutKey => String.IsNullOrWhiteSpace(LayoutKey) ? DashboardLayout.DefaultKey : LayoutKey;

    private IEnumerable<DashboardArrangedPart> ListedParts => IsEditing ? Arranged : Arranged.Where(part => !part.Hidden);

    private string AnnouncementText => AnnouncementFlip ? Announcement + "\u00A0" : Announcement;

    private string PersonalizeButtonId => $"rc-{InstanceId}-personalize";

    private string EditTitleId => $"rc-{InstanceId}-edit-title";

    private string ResetButtonId => $"rc-{InstanceId}-reset";

    private string KeepButtonId => $"rc-{InstanceId}-keep";

    private string ConfirmTextId => $"rc-{InstanceId}-confirm-text";

    private string TitleOf(DashboardPartRegistration registration) =>
        String.IsNullOrWhiteSpace(registration.Title) ? HumanizeId(registration.Id) : registration.Title;

    private static string HumanizeId(string id)
    {
        var words = id.Replace('-', ' ').Replace('_', ' ').Trim();
        return words.Length == 0 ? id : Char.ToUpperInvariant(words[0]) + words[1..];
    }

    private async Task LoadLayoutAsync()
    {
        Store = ServiceProvider.GetService<IDashboardLayoutStore>();

        if (Store is null)
            return;

        try
        {
            var timeout = Settings.LayoutLoadTimeout > TimeSpan.Zero ? Settings.LayoutLoadTimeout : Timeout.InfiniteTimeSpan;
            using var timeoutSource = new CancellationTokenSource(timeout, TimeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(DisposalSource.Token, timeoutSource.Token);
            Layout = await Store.GetAsync(EffectiveLayoutKey, linked.Token) ?? new DashboardLayout();
        }
        catch (Exception exception)
        {
            Layout = new DashboardLayout();

            if (!DisposalSource.IsCancellationRequested)
                LogStoreFailure(exception, "loading");
        }
    }

    private void RefreshArrangement() =>
        Arranged = [.. DashboardLayoutArranger.Arrange(VisibleParts, IsEditing ? DashboardLayoutArranger.ToLayout(Arranged) : Layout)];

    private void StartEditing()
    {
        if (!IsPersonalizable || IsEditing)
            return;

        IsEditing = true;
        EditError = null;
        IsConfirmingReset = false;
        PendingFocusId = EditTitleId;
        Announce(Localizer["PersonalizeStarted"].Value);
    }

    private void Move(string partId, int direction)
    {
        var index = Arranged.FindIndex(part => part.Registration.Id == partId);

        if (index < 0)
            return;

        var title = TitleOf(Arranged[index].Registration);
        var target = index + direction;
        PendingControlFocus = (partId, direction < 0 ? "up" : "down");

        if (target < 0 || target >= Arranged.Count)
        {
            Announce(Localizer[direction < 0 ? "PartAlreadyFirst" : "PartAlreadyLast", title].Value);
            return;
        }

        (Arranged[index], Arranged[target]) = (Arranged[target], Arranged[index]);
        Announce(String.Format(Localizer["PartMoved"].Value, title, target + 1, Arranged.Count));
    }

    private void ToggleHidden(string partId)
    {
        var index = Arranged.FindIndex(part => part.Registration.Id == partId);

        if (index < 0)
            return;

        var current = Arranged[index];
        Arranged[index] = current with { Hidden = !current.Hidden };
        PendingControlFocus = (partId, "visibility");
        Announce(Localizer[current.Hidden ? "PartShownAnnouncement" : "PartHiddenAnnouncement", TitleOf(current.Registration)].Value);
    }

    private async Task SaveAsync()
    {
        if (Store is null || IsSaving)
            return;

        IsSaving = true;
        EditError = null;
        var saved = DashboardLayoutArranger.ToLayout(Arranged);

        try
        {
            await Store.SaveAsync(EffectiveLayoutKey, saved, DisposalSource.Token);
        }
        catch (Exception exception)
        {
            IsSaving = false;

            if (DisposalSource.IsCancellationRequested)
                return;

            LogStoreFailure(exception, "saving");
            EditError = Localizer["LayoutSaveFailed"].Value;
            Announce(EditError);
            return;
        }

        IsSaving = false;
        Layout = saved;
        LeaveEditing(Localizer["LayoutSaved"].Value);
    }

    private void CancelEditing()
    {
        if (IsSaving)
            return;

        LeaveEditing(Localizer["PersonalizeCancelled"].Value);
    }

    private void AskReset()
    {
        if (IsSaving)
            return;

        IsConfirmingReset = true;
        PendingFocusId = KeepButtonId;
    }

    private void KeepLayout()
    {
        IsConfirmingReset = false;
        PendingFocusId = ResetButtonId;
    }

    private async Task ConfirmResetAsync()
    {
        if (Store is null || IsSaving)
            return;

        IsSaving = true;
        EditError = null;

        try
        {
            await Store.ResetAsync(EffectiveLayoutKey, DisposalSource.Token);
        }
        catch (Exception exception)
        {
            IsSaving = false;

            if (DisposalSource.IsCancellationRequested)
                return;

            LogStoreFailure(exception, "resetting");
            IsConfirmingReset = false;
            EditError = Localizer["LayoutResetFailed"].Value;
            Announce(EditError);
            return;
        }

        IsSaving = false;
        Layout = new DashboardLayout();
        LeaveEditing(Localizer["LayoutReset"].Value);
    }

    private void LeaveEditing(string announcement)
    {
        IsEditing = false;
        IsConfirmingReset = false;
        EditError = null;
        PendingControlFocus = null;
        PendingFocusId = PersonalizeButtonId;
        RefreshArrangement();
        Announce(announcement);
    }

    private void Announce(string text)
    {
        Announcement = text;
        AnnouncementFlip = !AnnouncementFlip;
    }

    private async Task ApplyPendingFocusAsync()
    {
        var control = PendingControlFocus;
        var id = PendingFocusId;
        PendingControlFocus = null;
        PendingFocusId = null;

        if (control is { } target)
            await Interop.FocusControlAsync(target.PartId, target.Action);
        else if (id is not null)
            await Interop.FocusByIdAsync(id);
    }

    private void LogStoreFailure(Exception exception, string operation) =>
        ServiceProvider.GetService<ILogger<RoleCenter>>()?.LogWarning(exception, "The dashboard layout store failed while {Operation} the layout of {DashboardKey}.", operation, EffectiveLayoutKey);
}
