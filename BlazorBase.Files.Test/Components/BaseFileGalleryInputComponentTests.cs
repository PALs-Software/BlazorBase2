using BlazorBase.Files.Attributes;
using BlazorBase.Files.Components.BaseFileGalleryInput;
using BlazorBase.Files.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.Files.Test.Components;

/// <summary>
/// The gallery's upload area must open the file picker when it is tapped. Fluent UI lays its content over the hidden
/// file input, so a tap only reaches the input through the anchor button; without it, phones (no drag and drop) could
/// not add a single file.
/// </summary>
public class BaseFileGalleryInputComponentTests : FilesBunitTestContextBase
{
    [Fact]
    public void WhenEditing_TheUploadAreaIsAButtonThatOpensThePicker()
    {
        var cut = RenderInput(isEditing: true, readOnly: false);

        var button = cut.Find("button.upload-area");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.False(string.IsNullOrEmpty(button.Id));
        var picker = cut.FindComponent<FluentInputFile>().Instance;
        Assert.Equal(button.Id, picker.AnchorId);
        Assert.True(picker.Multiple);
    }

    [Fact]
    public void WhenReadOnly_OffersNoFilePicker()
    {
        var cut = RenderInput(isEditing: true, readOnly: true);

        Assert.Empty(cut.FindComponents<FluentInputFile>());
        Assert.Empty(cut.FindAll("button.upload-area"));
    }

    private IRenderedComponent<BaseFileGalleryInputComponent> RenderInput(bool isEditing, bool readOnly)
    {
        var property = typeof(GalleryHolder).GetProperty(nameof(GalleryHolder.PhotoIds))!;

        return Render<BaseFileGalleryInputComponent>(parameters => parameters
            .Add(p => p.Model, new GalleryHolder())
            .Add(p => p.Property, property)
            .Add(p => p.Value, new List<Guid>())
            .Add(p => p.IsEditing, isEditing)
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<object?>(this, _ => { })));
    }

    private sealed class GalleryHolder
    {
        [AllowCameraCapture]
        public List<Guid> PhotoIds { get; set; } = [];
    }
}
