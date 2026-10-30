using BlazorBase.Dashboard.Test.Infrastructure;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Dashboard.Test.Personalization;

public sealed class ClientStoreTests : DashboardTestContext
{
    private static DashboardLayout Layout() => new() { Parts = [new DashboardPartPreference { PartId = "a", Hidden = true, Order = 0 }] };

    [Fact]
    public async Task InMemoryStore_SavesReadsAndResetsPerKey()
    {
        var store = new InMemoryDashboardLayoutStore();

        Assert.True((await store.GetAsync("/")).IsDefault);

        await store.SaveAsync("/", Layout());
        await store.SaveAsync("/other", new DashboardLayout());

        Assert.True(Assert.Single((await store.GetAsync("/")).Parts).Hidden);
        Assert.True((await store.GetAsync("/other")).IsDefault);

        await store.ResetAsync("/");
        await store.ResetAsync("/never-saved");

        Assert.True((await store.GetAsync("/")).IsDefault);
    }

    [Fact]
    public async Task InMemoryStore_ReturnsCopies_SoCallersCannotChangeTheStoredLayout()
    {
        var store = new InMemoryDashboardLayoutStore();
        await store.SaveAsync("/", Layout());

        (await store.GetAsync("/")).Parts[0].Hidden = false;

        Assert.True((await store.GetAsync("/")).Parts[0].Hidden);
    }

    [Fact]
    public async Task InMemoryStore_RejectsInvalidInput()
    {
        var store = new InMemoryDashboardLayoutStore();

        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => store.GetAsync(" "));
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => store.SaveAsync("/", new DashboardLayout { Parts = [new DashboardPartPreference { PartId = "" }] }));
    }

    [Fact]
    public async Task LocalStorageStore_WritesUnderAPrefixedKey_AndRemovesItOnReset()
    {
        var store = new LocalStorageDashboardLayoutStore(JSInterop.JSRuntime);
        var write = JSInterop.SetupVoid("localStorage.setItem", invocation => invocation.Arguments[0]!.ToString() == "blazorbase.dashboardlayouts:/");
        var remove = JSInterop.SetupVoid("localStorage.removeItem", invocation => invocation.Arguments[0]!.ToString() == "blazorbase.dashboardlayouts:/");
        write.SetVoidResult();
        remove.SetVoidResult();

        await store.SaveAsync("/", Layout());
        await store.ResetAsync("/");

        var saved = Assert.Single(write.Invocations);
        Assert.Contains("\"partId\":\"a\"", saved.Arguments[1]!.ToString());
        Assert.Single(remove.Invocations);
    }

    [Fact]
    public async Task LocalStorageStore_ReadsTheStoredJson_AndFallsBackToDefaultWhenNothingIsStored()
    {
        var store = new LocalStorageDashboardLayoutStore(JSInterop.JSRuntime);
        JSInterop.Setup<string?>("localStorage.getItem", "blazorbase.dashboardlayouts:/").SetResult("[{\"partId\":\"a\",\"hidden\":true,\"order\":2}]");
        JSInterop.Setup<string?>("localStorage.getItem", "blazorbase.dashboardlayouts:/empty").SetResult(null);

        Assert.True(Assert.Single((await store.GetAsync("/")).Parts).Hidden);
        Assert.True((await store.GetAsync("/empty")).IsDefault);
    }

    [Fact]
    public void ClientRegistrations_AddTheStore()
    {
        var services = new ServiceCollection();
        services.AddBlazorBaseDashboardLayoutsInMemory();
        services.AddBlazorBaseDashboardLayoutsLocalStorage();
        services.AddBlazorBaseDashboardLayoutsClient(client => client.BaseAddress = new Uri("http://localhost/api/dashboard-layouts/"));

        Assert.Equal(3, services.Count(descriptor => descriptor.ServiceType == typeof(IDashboardLayoutStore)));
    }
}
