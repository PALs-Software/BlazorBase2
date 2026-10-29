using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Components.Test.Dialogs;

/// <summary>
/// The confirmation that stands between a person and an irreversible action: it has to look different from a
/// plain question and must not hand the keyboard to the button that destroys data.
/// </summary>
public class BaseConfirmationDialogTests : ComponentsBunitTestContextBase
{
    [Fact]
    public void ADestructiveConfirmation_ShowsAWarningIconAndADangerStyledConfirmButton()
    {
        var dialog = Open(isDestructive: true);

        Assert.NotEmpty(dialog.Content.FindAll(".base-confirmation-message svg"));
        var danger = dialog.Content.FindComponents<FluentButton>().Single(button => button.Instance.Class?.Contains("base-danger-action") == true);
        Assert.Equal("Delete", danger.Caption());
    }

    [Fact]
    public void APlainConfirmation_HasNeitherTheIconNorTheDangerStyle()
    {
        var dialog = Open(isDestructive: false);

        Assert.Empty(dialog.Content.FindAll(".base-confirmation-message svg"));
        Assert.Empty(dialog.Content.FindAll(".base-danger-action"));
    }

    [Fact]
    public void FocusStartsOnCancel_SoAStrayEnterCannotConfirm()
    {
        var dialog = Open(isDestructive: true);

        var focusTarget = dialog.Content.FindComponents<FluentButton>().Single(button => button.Markup.Contains("data-autofocus"));

        Assert.Equal("Cancel", focusTarget.Caption());
    }

    [Fact]
    public void ThePromptNamesWhatIsAboutToBeDeleted()
    {
        var dialog = Open(isDestructive: true, message: "Do you really want to delete \"Quarterly report\"?");

        Assert.Contains("Quarterly report", dialog.Content.Find(".base-confirmation-text").TextContent);
    }

    [Fact]
    public async Task ConfirmingClosesTheDialogWithAnAcceptedResult()
    {
        var dialog = Open(isDestructive: false);

        dialog.Content.FindComponents<FluentButton>().Single(button => button.Caption() == "Delete").Click();

        Assert.False((await dialog.Result).Cancelled);
    }

    [Fact]
    public async Task CancellingClosesTheDialogWithACancelledResult()
    {
        var dialog = Open(isDestructive: false);

        dialog.Content.FindComponents<FluentButton>().Single(button => button.Caption() == "Cancel").Click();

        Assert.True((await dialog.Result).Cancelled);
    }

    [Fact]
    public async Task AServiceWithoutADestructivePresentation_FallsBackToThePlainConfirmation()
    {
        IConfirmationService service = new PlainOnlyConfirmationService();

        var confirmed = await service.ConfirmDestructiveAsync("Delete it?", "Delete", "Delete", "Cancel");

        Assert.True(confirmed);
    }

    private DialogHarness<BaseConfirmationDialog> Open(bool isDestructive, string message = "Delete it?")
        => DialogHarness<BaseConfirmationDialog>.Open(this, "Delete", new ConfirmationDialogContent
        {
            Message = message,
            ConfirmButtonText = "Delete",
            CancelButtonText = "Cancel",
            IsDestructive = isDestructive
        });

    private sealed class PlainOnlyConfirmationService : IConfirmationService
    {
        public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText, string cancelButtonText)
            => Task.FromResult(true);
    }
}
