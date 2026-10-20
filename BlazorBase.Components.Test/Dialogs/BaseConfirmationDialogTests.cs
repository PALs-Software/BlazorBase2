using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Services;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
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
        var cut = Render(isDestructive: true);

        Assert.NotEmpty(cut.FindAll(".base-confirmation-message svg"));
        Assert.Equal("Delete", cut.Find("fluent-button.base-confirmation-danger").TextContent.Trim());
    }

    [Fact]
    public void APlainConfirmation_HasNeitherTheIconNorTheDangerStyle()
    {
        var cut = Render(isDestructive: false);

        Assert.Empty(cut.FindAll(".base-confirmation-message svg"));
        Assert.Empty(cut.FindAll(".base-confirmation-danger"));
    }

    [Fact]
    public void FocusStartsOnCancel_SoAStrayEnterCannotConfirm()
    {
        var cut = Render(isDestructive: true);

        var focusTarget = cut.Find("[data-autofocus]");

        Assert.Equal("Cancel", focusTarget.TextContent.Trim());
    }

    [Fact]
    public void ThePromptNamesWhatIsAboutToBeDeleted()
    {
        var cut = Render(isDestructive: true, message: "Do you really want to delete \"Quarterly report\"?");

        Assert.Contains("Quarterly report", cut.Find(".base-confirmation-text").TextContent);
    }

    [Fact]
    public async Task AServiceWithoutADestructivePresentation_FallsBackToThePlainConfirmation()
    {
        IConfirmationService service = new PlainOnlyConfirmationService();

        var confirmed = await service.ConfirmDestructiveAsync("Delete it?", "Delete", "Delete", "Cancel");

        Assert.True(confirmed);
    }

    private IRenderedComponent<BaseConfirmationDialog> Render(bool isDestructive, string message = "Delete it?")
        => Render<BaseConfirmationDialog>(parameters => parameters.Add(p => p.Content, new ConfirmationDialogContent
        {
            Message = message,
            ConfirmButtonText = "Delete",
            CancelButtonText = "Cancel",
            IsDestructive = isDestructive
        }));

    private sealed class PlainOnlyConfirmationService : IConfirmationService
    {
        public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText, string cancelButtonText)
            => Task.FromResult(true);
    }
}
