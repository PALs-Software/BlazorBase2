using System.Net;
using BlazorBase.Components.Diagnostics;
using Bunit;
using BlazorBase.Components.Test.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Components.Test.Diagnostics;

public sealed class ClientDiagnosticsTests
{
    private static ClientDiagnostics Create(Action<ClientDiagnosticsOptions>? configure = null)
    {
        var options = new ClientDiagnosticsOptions();
        configure?.Invoke(options);

        return new ClientDiagnostics(Options.Create(options));
    }

    [Fact]
    public void TheBufferKeepsTheNewestEntriesUpToTheCapacity()
    {
        var diagnostics = Create(options => options.Capacity = 3);

        for (var index = 1; index <= 5; index++)
            diagnostics.RecordError("Test", $"error {index}");

        Assert.Equal(["error 3", "error 4", "error 5"], diagnostics.Errors.Select(entry => entry.Message));
    }

    [Fact]
    public void ExceptionsAreRecordedAsTypeAndMessageWithoutTheStack()
    {
        var diagnostics = Create();

        diagnostics.RecordError("ErrorBoundary", new InvalidOperationException("broken"));

        var entry = Assert.Single(diagnostics.Errors);
        Assert.Equal("InvalidOperationException: broken", entry.Message);
        Assert.Equal("ErrorBoundary", entry.Source);
    }

    [Fact]
    public void LongMultilineMessagesAreFlattenedAndCut()
    {
        var diagnostics = Create(options => options.MaxMessageLength = 20);

        diagnostics.RecordError("Test", "line one\nline two is quite long");

        var message = Assert.Single(diagnostics.Errors).Message;
        Assert.Equal(20, message.Length);
        Assert.DoesNotContain('\n', message);
        Assert.EndsWith("…", message);
    }

    [Fact]
    public void BlankMessagesAndCorrelationIdsAreIgnored()
    {
        var diagnostics = Create();

        diagnostics.RecordError("Test", "  ");
        diagnostics.RecordCorrelationId(" ");

        Assert.Empty(diagnostics.Errors);
        Assert.Null(diagnostics.LastCorrelationId);
    }

    [Fact]
    public void TheLastCorrelationIdWinsAndChangesAreAnnounced()
    {
        var diagnostics = Create();
        var changes = 0;
        diagnostics.Changed += () => changes++;

        diagnostics.RecordCorrelationId("a");
        diagnostics.RecordCorrelationId("b");
        diagnostics.RecordError("Test", "x");

        Assert.Equal("b", diagnostics.LastCorrelationId);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var diagnostics = Create();
        diagnostics.RecordError("Test", "x");
        diagnostics.RecordCorrelationId("a");

        diagnostics.Clear();

        Assert.Empty(diagnostics.Errors);
        Assert.Null(diagnostics.LastCorrelationId);
    }

    [Fact]
    public void TheLoggerProviderRecordsErrorsButNotWarnings()
    {
        var diagnostics = Create();
        var options = new ClientDiagnosticsOptions();
        using var provider = new ClientDiagnosticsLoggerProvider(diagnostics, options);
        var logger = provider.CreateLogger("Demo.Category");

        logger.LogWarning("just a warning");
        logger.LogError(new InvalidOperationException("boom"), "it failed");

        var entry = Assert.Single(diagnostics.Errors);
        Assert.Equal("Logger", entry.Source);
        Assert.Equal("Demo.Category: it failed (InvalidOperationException: boom)", entry.Message);
    }

    [Fact]
    public async Task TheHandlerRemembersTheCorrelationIdAndRecordsServerErrorsWithoutTheQuery()
    {
        var diagnostics = Create();
        var inner = new StubHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            response.Headers.Add("X-Correlation-ID", "corr-42");

            return response;
        });
        var handler = new ClientDiagnosticsHandler(diagnostics, Options.Create(new ClientDiagnosticsOptions())) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://demo.example.com") };

        await client.GetAsync("/api/items?secret=1");

        Assert.Equal("corr-42", diagnostics.LastCorrelationId);
        Assert.Equal("GET /api/items returned 500", Assert.Single(diagnostics.Errors).Message);
    }

    [Fact]
    public async Task TheHandlerRecordsTransportFailuresAndRethrows()
    {
        var diagnostics = Create();
        var inner = new StubHandler(_ => throw new HttpRequestException("offline"));
        var handler = new ClientDiagnosticsHandler(diagnostics, Options.Create(new ClientDiagnosticsOptions())) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://demo.example.com") };

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/api/items"));

        Assert.Equal("GET /api/items failed: offline", Assert.Single(diagnostics.Errors).Message);
    }

    [Fact]
    public void TheRegistrationIsScopedOutsideTheBrowserSoCircuitsAreIsolated()
    {
        var services = new ServiceCollection().AddBlazorBaseClientDiagnostics();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.NotSame(first.ServiceProvider.GetRequiredService<IClientDiagnostics>(), second.ServiceProvider.GetRequiredService<IClientDiagnostics>());
        Assert.NotNull(first.ServiceProvider.GetRequiredService<ClientDiagnosticsHandler>());
    }

    [Fact]
    public void AnExplicitSingletonLifetimeSharesTheBufferAndHooksTheLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBaseClientDiagnostics(lifetime: ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ILoggerFactory>().CreateLogger("X").LogError("shared failure");

        Assert.Same(provider.GetRequiredService<IClientDiagnostics>(), provider.CreateScope().ServiceProvider.GetRequiredService<IClientDiagnostics>());
        Assert.Contains("shared failure", provider.GetRequiredService<IClientDiagnostics>().Errors.Single().Message);
    }

    [Fact]
    public void ConfigurationFromASecondCallStillApplies()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseClientDiagnostics();
        services.AddBlazorBaseClientDiagnostics(options => options.Capacity = 2);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var diagnostics = scope.ServiceProvider.GetRequiredService<IClientDiagnostics>();

        diagnostics.RecordError("T", "1");
        diagnostics.RecordError("T", "2");
        diagnostics.RecordError("T", "3");

        Assert.Equal(2, diagnostics.Errors.Count);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

public sealed class DiagnosticsErrorBoundaryTests : ComponentsBunitTestContextBase
{
    [Fact]
    public void AnExceptionInTheContentIsRecordedAndTheBoundaryStillShowsItsErrorContent()
    {
        var diagnostics = new ClientDiagnostics(Options.Create(new ClientDiagnosticsOptions()));
        Services.AddSingleton<IClientDiagnostics>(diagnostics);

        var cut = Render<DiagnosticsErrorBoundary>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<Thrower>(0);
                builder.CloseComponent();
            }))
            .Add(p => p.ErrorContent, (RenderFragment<Exception>)(exception => builder => builder.AddContent(0, "failed: " + exception.Message))));

        Assert.Contains("failed: kaboom", cut.Markup);
        var entry = Assert.Single(diagnostics.Errors);
        Assert.Equal("ErrorBoundary", entry.Source);
        Assert.Equal("InvalidOperationException: kaboom", entry.Message);
    }

    [Fact]
    public void WithoutRegisteredDiagnosticsTheBoundaryStillWorks()
    {
        var cut = Render<DiagnosticsErrorBoundary>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<Thrower>(0);
                builder.CloseComponent();
            }))
            .Add(p => p.ErrorContent, (RenderFragment<Exception>)(_ => builder => builder.AddContent(0, "handled"))));

        Assert.Contains("handled", cut.Markup);
    }

    private sealed class Thrower : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => throw new InvalidOperationException("kaboom");
    }
}
