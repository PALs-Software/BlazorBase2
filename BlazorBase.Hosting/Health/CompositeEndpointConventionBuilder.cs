using Microsoft.AspNetCore.Builder;

namespace BlazorBase.Hosting.Health;

internal sealed class CompositeEndpointConventionBuilder(IReadOnlyList<IEndpointConventionBuilder> builders) : IEndpointConventionBuilder
{
    #region Injects
    private readonly IReadOnlyList<IEndpointConventionBuilder> Builders = builders;
    #endregion

    public void Add(Action<EndpointBuilder> convention)
    {
        foreach (var builder in Builders)
            builder.Add(convention);
    }

    public void Finally(Action<EndpointBuilder> finalConvention)
    {
        foreach (var builder in Builders)
            builder.Finally(finalConvention);
    }
}
