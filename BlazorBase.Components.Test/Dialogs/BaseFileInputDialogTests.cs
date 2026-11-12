using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Components.Test.Dialogs;

/// <summary>
/// The file input dialog shows the caller's texts and offers a file picker limited to the accepted file types.
/// </summary>
public class BaseFileInputDialogTests : ComponentsBunitTestContextBase
{
    public BaseFileInputDialogTests() => Services.AddLocalization();

    [Fact]
    public void TheDialogShowsTheMessageAndThePickerCaption()
    {
        var dialog = Open();

        Assert.Contains("Choose the file to import.", dialog.Content.Markup);
        Assert.Contains("Choose a file", dialog.Content.Find(".base-file-input-dialog-drop-area").TextContent);
    }

    [Fact]
    public void ThePickerAcceptsOnlyTheConfiguredFileTypes()
    {
        var dialog = Open();

        var picker = dialog.Content.FindComponent<FluentInputFile>().Instance;
        Assert.Equal(".csv", picker.Accept);
        Assert.False(picker.Multiple);
    }

    private DialogHarness<BaseFileInputDialog> Open()
        => DialogHarness<BaseFileInputDialog>.Open(this, "Import", new FileInputDialogContent
        {
            Message = "Choose the file to import.",
            FileInputCaption = "Choose a file",
            FileFilter = ".csv",
            MaxFileSize = 0
        });
}
