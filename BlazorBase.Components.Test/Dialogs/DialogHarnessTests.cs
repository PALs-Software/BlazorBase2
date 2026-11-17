using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Test.Infrastructure;
using Xunit;

namespace BlazorBase.Components.Test.Dialogs;

/// <summary>
/// A dialog whose content cannot be rendered must fail its test with the reason. Searching the render tree after
/// such a failure used to overflow the stack in bUnit and kill the whole test host.
/// </summary>
public class DialogHarnessTests : ComponentsBunitTestContextBase
{
    [Fact]
    public void ADialogWithAMissingService_FailsWithTheReasonInsteadOfCrashingTheTestHost()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DialogHarness<BaseFileInputDialog>.Open(this, "Import", new FileInputDialogContent
            {
                Message = "Choose the file to import.",
                FileInputCaption = "Choose a file",
                FileFilter = ".csv",
                MaxFileSize = 0
            }));

        Assert.Contains(nameof(BaseFileInputDialog), exception.Message);
        Assert.Contains("DialogLocalizer", exception.Message);
    }
}
