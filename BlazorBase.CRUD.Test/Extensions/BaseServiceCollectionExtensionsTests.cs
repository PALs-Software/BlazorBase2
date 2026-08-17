using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.CustomProperties;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.CRUD.Test.Extensions;

public class BaseServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlazorBaseCrud_RegistersValidationServiceOpenGeneric()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseCrud();

        Assert.Contains(services, d => d.ServiceType == typeof(BaseValidationService<>)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddBlazorBaseCrudWithAuditProvider_RegistersAuditUserProvider()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseCrud<StubAuditUserProvider>();

        Assert.Contains(services, d => d.ServiceType == typeof(IAuditUserProvider)
            && d.ImplementationType == typeof(StubAuditUserProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(BaseValidationService<>));
    }

    [Fact]
    public void AddBlazorBaseCrudServer_RegistersDataProviderPerBaseCrudEntity()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseCrudServer<TestDbContext>(typeof(TestProduct).Assembly);

        Assert.Contains(services, d => d.ServiceType == typeof(IBaseDataProvider<TestProduct>));
        Assert.Contains(services, d => d.ServiceType == typeof(IBaseDataProvider<TestOrder>));
    }

    [Fact]
    public void AddBlazorBaseCrudServer_ResolvesDbContextBackedProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseSqlite("DataSource=:memory:"));
        services.AddBlazorBaseCrudServer<TestDbContext>(typeof(TestProduct).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var provider = scope.ServiceProvider.GetService<IBaseDataProvider<TestProduct>>();

        Assert.IsType<DbContextBaseDataProvider<TestProduct>>(provider);
    }

    [Fact]
    public void AddBaseDataInterceptor_RegistersScopedInterceptor()
    {
        var services = new ServiceCollection();

        services.AddBaseDataInterceptor<TestProduct, NoOpProductInterceptor>();

        Assert.Contains(services, d => d.ServiceType == typeof(IBaseDataInterceptor<TestProduct>)
            && d.ImplementationType == typeof(NoOpProductInterceptor)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddBaseValidator_RegistersScopedValidator()
    {
        var services = new ServiceCollection();

        services.AddBaseValidator<TestProduct, AlwaysInvalidProductValidator>();

        Assert.Contains(services, d => d.ServiceType == typeof(IBaseValidator<TestProduct>)
            && d.ImplementationType == typeof(AlwaysInvalidProductValidator)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddBlazorBaseCustomInput_RegistersScopedUnderInterface()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseCustomInput<SampleNameInput>();

        Assert.Contains(services, d => d.ServiceType == typeof(IBaseCustomPropertyInput)
            && d.ImplementationType == typeof(SampleNameInput)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddBlazorBaseCustomDisplay_RegistersScopedUnderInterface()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseCustomDisplay<SampleStatusDisplay>();

        Assert.Contains(services, d => d.ServiceType == typeof(IBaseCustomPropertyDisplay)
            && d.ImplementationType == typeof(SampleStatusDisplay)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddBlazorBaseCustomInput_MultipleResolveInRegistrationOrder()
    {
        var services = new ServiceCollection();

        services.AddBlazorBaseCustomInput<SampleNameInput>();
        services.AddBlazorBaseCustomInput<AlternateNameInput>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetServices<IBaseCustomPropertyInput>().ToList();

        Assert.Equal(2, resolved.Count);
        Assert.IsType<SampleNameInput>(resolved[0]);
        Assert.IsType<AlternateNameInput>(resolved[1]);
    }
}
