using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.Components.Test.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The search box itself: it reports the text after a pause in typing, once per distinct text, and a cleared box at once.
/// </summary>
public class BaseListQuickSearchBoxTests : BunitTestContextBase
{
    private const int PauseMilliseconds = 150;

    private readonly List<string> reported = [];

    private IRenderedComponent<BaseListQuickSearch> RenderBox(int debounceMilliseconds, string? value = null) =>
        Render<BaseListQuickSearch>(parameters => parameters
            .Add(p => p.Value, value)
            .Add(p => p.Placeholder, "Search...")
            .Add(p => p.ClearLabel, "Clear search")
            .Add(p => p.DebounceMilliseconds, debounceMilliseconds)
            .Add(p => p.OnSearch, EventCallback.Factory.Create<string>(this, text => { lock (reported) reported.Add(text); })));

    private List<string> Reported()
    {
        lock (reported)
            return [.. reported];
    }

    [Fact]
    public void Typing_ReportsNothingUntilTheDebounceHasPassed_ThenTheLatestTextOnce()
    {
        var cut = RenderBox(PauseMilliseconds);
        var input = cut.FindComponent<FluentTextInput>();

        var firstKey = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("w"));
        var secondKey = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("wi"));
        var thirdKey = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("wid"));

        Assert.Empty(Reported());

        Task.WaitAll(firstKey, secondKey, thirdKey);

        Assert.Equal(["wid"], Reported());
    }

    [Fact]
    public void WithoutADebounce_EveryChangeIsReportedAtOnce()
    {
        var cut = RenderBox(debounceMilliseconds: 0);
        var input = cut.FindComponent<FluentTextInput>();

        input.SetValue<string?>("a");
        input.SetValue<string?>("ab");

        Assert.Equal(["a", "ab"], Reported());
    }

    [Fact]
    public void TheTextIsReportedTrimmed_AndAChangeThatKeepsTheTextIsNotReportedAgain()
    {
        var cut = RenderBox(debounceMilliseconds: 0);
        var input = cut.FindComponent<FluentTextInput>();

        input.SetValue<string?>("  abc ");
        input.SetValue<string?>("abc");
        input.SetValue<string?>("   ");

        Assert.Equal(["abc", string.Empty], Reported());
    }

    [Fact]
    public void ClearingTheBox_ReportsAnEmptyTextAtOnce_AndDropsWhatWasStillPending()
    {
        var cut = RenderBox(PauseMilliseconds);
        var input = cut.FindComponent<FluentTextInput>();
        cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("abc")).GetAwaiter().GetResult();
        var pending = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("abcd"));

        cut.FindComponents<FluentButton>().Single(button => button.Instance.Class == "base-list-quick-search-clear").Click();
        pending.GetAwaiter().GetResult();

        Assert.Equal(["abc", string.Empty], Reported());
    }

    [Fact]
    public void TheClearButton_IsOnlyOfferedWhileThereIsText()
    {
        var cut = RenderBox(debounceMilliseconds: 0);

        Assert.Empty(cut.FindComponents<FluentButton>());

        cut.FindComponent<FluentTextInput>().SetValue<string?>("abc");

        Assert.Single(cut.FindComponents<FluentButton>());
        Assert.Equal("Clear search", cut.FindComponent<FluentButton>().Instance.Title);
    }

    [Fact]
    public void AValueFromTheOutside_ReplacesTheText_WhenNothingIsPending()
    {
        var cut = RenderBox(debounceMilliseconds: 0);
        cut.FindComponent<FluentTextInput>().SetValue<string?>("abc");

        cut.Render(parameters => parameters.Add(p => p.Value, "xyz"));

        Assert.Equal("xyz", cut.FindComponent<FluentTextInput>().Instance.Value);
    }

    [Fact]
    public void ARerenderWithTheOldValue_KeepsWhatIsBeingTyped()
    {
        var cut = RenderBox(PauseMilliseconds, value: "abc");
        var input = cut.FindComponent<FluentTextInput>();

        var typing = cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync("abcd"));
        cut.Render(parameters => parameters.Add(p => p.Placeholder, "Find..."));

        Assert.Equal("abcd", cut.FindComponent<FluentTextInput>().Instance.Value);
        typing.GetAwaiter().GetResult();
    }
}
