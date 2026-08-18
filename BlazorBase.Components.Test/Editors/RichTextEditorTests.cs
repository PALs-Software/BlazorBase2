using BlazorBase.Components.Sanitization;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using RichTextEditorComponent = BlazorBase.Components.Editors.RichTextEditor;

namespace BlazorBase.Components.Test.Editors;

public class RichTextEditorTests : ComponentsBunitTestContextBase
{
    private const string ModulePath = "./_content/BlazorBase.Components/js/richTextEditor.js";
    private const string MaliciousValue = "<b>ok</b><script>alert(1)</script>";
    private const string SanitizedValue = "<b>ok</b>";

    [Fact]
    public void SetHtml_ReceivesSanitizedValue_WhenHostSanitizerIsRegistered()
    {
        var sanitizer = Substitute.For<IHtmlSanitizer>();
        sanitizer.Sanitize(MaliciousValue).Returns(SanitizedValue);
        Services.AddSingleton(sanitizer);

        var module = SetupRichTextEditorModule();
        var setHtmlHandler = module.SetupVoid("setHtml", _ => true);
        setHtmlHandler.SetVoidResult();

        Render<RichTextEditorComponent>(parameters => parameters
            .Add(p => p.Value, MaliciousValue));

        sanitizer.Received(1).Sanitize(MaliciousValue);

        var invocation = Assert.Single(setHtmlHandler.Invocations);
        Assert.Equal(SanitizedValue, invocation.Arguments[1]);
    }

    [Fact]
    public void SetHtml_ReceivesRawValue_WhenNoHostSanitizerIsRegistered()
    {
        var module = SetupRichTextEditorModule();
        var setHtmlHandler = module.SetupVoid("setHtml", _ => true);
        setHtmlHandler.SetVoidResult();

        var exception = Record.Exception(() => Render<RichTextEditorComponent>(parameters => parameters
            .Add(p => p.Value, MaliciousValue)));

        Assert.Null(exception);

        var invocation = Assert.Single(setHtmlHandler.Invocations);
        Assert.Equal(MaliciousValue, invocation.Arguments[1]);
    }

    [Fact]
    public async Task OnContentChangedAsync_RaisesValueChangedWithSanitizedValue_WhenHostSanitizerIsRegistered()
    {
        var sanitizer = Substitute.For<IHtmlSanitizer>();
        sanitizer.Sanitize(MaliciousValue).Returns(SanitizedValue);
        Services.AddSingleton(sanitizer);

        var module = SetupRichTextEditorModule();
        module.SetupVoid("setHtml", _ => true).SetVoidResult();

        string? receivedValue = null;
        var cut = Render<RichTextEditorComponent>(parameters => parameters
            .Add(p => p.Value, string.Empty)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => receivedValue = value)));

        await cut.InvokeAsync(() => cut.Instance.OnContentChangedAsync(MaliciousValue));

        Assert.Equal(SanitizedValue, receivedValue);
    }

    private BunitJSModuleInterop SetupRichTextEditorModule()
    {
        var module = JSInterop.SetupModule(ModulePath);

        var initEditorHandler = module.SetupVoid("initEditor", _ => true);
        initEditorHandler.SetVoidResult();

        var setReadOnlyHandler = module.SetupVoid("setReadOnly", _ => true);
        setReadOnlyHandler.SetVoidResult();

        var destroyEditorHandler = module.SetupVoid("destroyEditor", _ => true);
        destroyEditorHandler.SetVoidResult();

        var getHtmlHandler = module.Setup<string>("getHtml", _ => true);
        getHtmlHandler.SetResult(string.Empty);

        return module;
    }
}
