namespace BlazorBase.Components.Help;

/// <summary>Finds the help topic of the page behind a URL.</summary>
public interface IHelpTopicResolver
{
    /// <summary>Returns the topic of the page whose route matches <paramref name="path"/>, or null when it has none.</summary>
    /// <param name="path">The path relative to the application's base address.</param>
    string? ResolveTopic(string? path);

    /// <summary>Returns the route (with a leading <c>/</c>) of the page that carries <paramref name="topic"/> and needs no route parameters, or null.</summary>
    string? GetRoute(string topic);
}
