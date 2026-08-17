using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// A no-op interceptor used by the DI registration tests.
/// </summary>
public sealed class NoOpProductInterceptor : BaseDataInterceptor<TestProduct>;
