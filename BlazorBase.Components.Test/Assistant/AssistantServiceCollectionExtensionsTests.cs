using BlazorBase.Components.Assistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorBase.Components.Test.Assistant;

public sealed class AssistantServiceCollectionExtensionsTests
{
    [Fact]
    public void TheRegistration_ProvidesTheServicesAndTheSseClientByDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HttpClient());
        services.AddBlazorBaseAssistant(options => options.SiteId = "site");

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.Equal("site", scope.ServiceProvider.GetRequiredService<IOptions<AssistantClientOptions>>().Value.SiteId);
        Assert.IsType<SseAssistantChatClient>(scope.ServiceProvider.GetRequiredService<IAssistantChatClient>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<AssistantPanelState>(), scope.ServiceProvider.GetRequiredService<AssistantPanelState>());
        Assert.NotSame(scope.ServiceProvider.GetRequiredService<AssistantPanelState>(), provider.CreateScope().ServiceProvider.GetRequiredService<AssistantPanelState>());
    }

    [Fact]
    public void AClientRegisteredBefore_IsKept()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAssistantChatClient, ScriptedAssistantChatClient>();
        services.AddBlazorBaseAssistant();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<ScriptedAssistantChatClient>(scope.ServiceProvider.GetRequiredService<IAssistantChatClient>());
    }

    [Fact]
    public void AnOptionsInstance_IsUsedAsIs()
    {
        var options = new AssistantClientOptions { DisplayName = "acadon Hub", UploadEnabled = true };
        var services = new ServiceCollection();
        services.AddBlazorBaseAssistant(options);

        using var provider = services.BuildServiceProvider();

        Assert.Same(options, provider.GetRequiredService<IOptions<AssistantClientOptions>>().Value);
    }

    [Fact]
    public void TheDefaults_KeepUploadOffAndTheContextSmall()
    {
        var options = new AssistantClientOptions();

        Assert.False(options.UploadEnabled);
        Assert.Equal(4096, options.MaxContextBytes);
        Assert.Equal("/api/agenthub/chat", options.Endpoint);
        Assert.Empty(options.Suggestions);
    }

    [Fact]
    public async Task TheScriptedClient_AnswersInOrderRecordsEverythingAndFallsBackToAnEcho()
    {
        var client = new ScriptedAssistantChatClient().ReplyWithText("First answer");

        var first = new List<AssistantStreamEvent>();
        await foreach (var streamEvent in client.SendAsync(new AssistantMessageRequest { Text = "q1" }))
            first.Add(streamEvent);

        var second = new List<AssistantStreamEvent>();
        await foreach (var streamEvent in client.SendAsync(new AssistantMessageRequest { Text = "q2" }))
            second.Add(streamEvent);

        Assert.Equal("First answer", string.Concat(first.OfType<AssistantTextDeltaEvent>().Select(delta => delta.Text)));
        Assert.Contains("q2", string.Concat(second.OfType<AssistantTextDeltaEvent>().Select(delta => delta.Text)));
        Assert.IsType<AssistantFinishedEvent>(second[^1]);
        Assert.Equal(["q1", "q2"], client.Requests.Select(request => request.Text));
    }

    [Fact]
    public async Task TheScriptedClient_StopsWhenTheTokenIsCancelled()
    {
        var client = new ScriptedAssistantChatClient().ReplyWithText("one two three");
        using var source = new CancellationTokenSource();
        var seen = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.SendAsync(new AssistantMessageRequest { Text = "q" }, source.Token))
            {
                seen++;
                await source.CancelAsync();
            }
        });

        Assert.Equal(1, seen);
    }
}
