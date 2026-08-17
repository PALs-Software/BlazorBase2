using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

[Collection(CustomPropertyResolutionCollection.Name)]
public class CardSaveParticipantTests : BunitTestContextBase
{
    [Fact]
    public void SaveParticipant_BlocksSave_WhenValidateReturnsFalse()
    {
        var recorder = new SaveParticipantRecorder { ValidationResult = false };
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        RegisterServices(recorder, provider);

        var cut = RenderCard(new TestProduct { Name = "P", Description = "D" }, provider);

        cut.Find("form").Submit();

        Assert.Equal(["ValidateAsync"], recorder.Calls);
        provider.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public void SaveParticipant_RunsLifecycleInOrder_OnSave()
    {
        var recorder = new SaveParticipantRecorder { ValidationResult = true };
        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.CreateAsync(Arg.Any<TestProduct>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<TestProduct>()));
        RegisterServices(recorder, provider);

        var model = new TestProduct { Name = "P", Description = "D" };
        var cut = RenderCard(model, provider);

        Assert.NotEmpty(cut.FindComponents<SaveParticipantInput>());

        cut.Find("form").Submit();

        Assert.Equal(["ValidateAsync", "OnBeforeSaveAsync", "OnAfterSaveAsync"], recorder.Calls);
        provider.Received(1).CreateAsync(Arg.Any<TestProduct>(), Arg.Any<CancellationToken>());
        Assert.NotNull(recorder.BeforeSaveContext);
        Assert.True(recorder.BeforeSaveContext!.IsCreate);
        Assert.Same(model, recorder.BeforeSaveContext.Model);
    }

    private void RegisterServices(SaveParticipantRecorder recorder, IBaseDataProvider<TestProduct> provider)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddSingleton(recorder);
        Services.AddSingleton(provider);
        Services.AddBlazorBaseCustomInput<SaveParticipantInput>();
    }

    private IRenderedComponent<BaseCard<TestProduct>> RenderCard(
        TestProduct model,
        IBaseDataProvider<TestProduct> provider)
    {
        var configuration = new BaseCardBuilder<TestProduct>()
            .Field(p => p.Description)
            .Build();

        return Render<BaseCard<TestProduct>>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.DataProvider, provider)
            .Add(p => p.Configuration, configuration));
    }
}
