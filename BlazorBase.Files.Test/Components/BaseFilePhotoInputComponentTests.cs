using System.Reflection;
using BlazorBase.Components.Test.Infrastructure;
using BlazorBase.Files.Attributes;
using BlazorBase.Files.Components.BaseFilePhotoInput;
using BlazorBase.Files.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.Files.Test.Components;

public class BaseFilePhotoInputComponentTests : FilesBunitTestContextBase
{
    [Fact]
    public void WhenEditing_OffersTheFilePickerWithTheCameraFilter()
    {
        var cut = RenderInput(value: null, isEditing: true, readOnly: false);

        Assert.Equal("image/*", cut.FindComponent<FluentInputFile>().Instance.Accept);
    }

    [Fact]
    public void WhenReadOnly_OffersNoFilePicker()
    {
        var cut = RenderInput(value: null, isEditing: true, readOnly: true);

        Assert.Empty(cut.FindComponents<FluentInputFile>());
    }

    [Fact]
    public void WithAFile_ShowsTheRemoveButton_OnlyWhileEditing()
    {
        using var english = new EnglishCulture();
        var editing = RenderInput(Guid.NewGuid(), isEditing: true, readOnly: false);
        var viewing = RenderInput(Guid.NewGuid(), isEditing: false, readOnly: false);

        var remove = Assert.Single(editing.FindComponents<FluentButton>()).Instance;
        Assert.True(remove.IconOnly);
        Assert.Equal("Remove file", remove.Title);
        Assert.Empty(viewing.FindComponents<FluentButton>());
    }

    [Fact]
    public async Task ClickingRemove_DeletesTheFile_AndClearsTheValue()
    {
        var fileId = Guid.NewGuid();
        object? emitted = "sentinel";
        var cut = RenderInput(fileId, isEditing: true, readOnly: false, onValueChanged: value => emitted = value);

        cut.FindComponent<FluentButton>().Click();

        await UploadClient.Received(1).DeleteAsync(fileId, Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => Assert.Null(emitted));
    }

    private IRenderedComponent<BaseFilePhotoInputComponent> RenderInput(
        Guid? value,
        bool isEditing,
        bool readOnly,
        Action<object?>? onValueChanged = null)
    {
        var property = typeof(PhotoHolder).GetProperty(nameof(PhotoHolder.PhotoId))!;

        return Render<BaseFilePhotoInputComponent>(parameters => parameters
            .Add(p => p.Model, new PhotoHolder())
            .Add(p => p.Property, property)
            .Add(p => p.Value, value)
            .Add(p => p.IsEditing, isEditing)
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, changed => onValueChanged?.Invoke(changed))));
    }

    private sealed class PhotoHolder
    {
        [AllowCameraCapture]
        public Guid PhotoId { get; set; }
    }
}
