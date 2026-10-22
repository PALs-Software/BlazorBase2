namespace BlazorBase.Components.Server.Help;

/// <summary>Settings of <see cref="HelpContentEndpointMapper.MapBlazorBaseHelpContent(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder, Action{HelpContentEndpointOptions})"/>.</summary>
public sealed class HelpContentEndpointOptions
{
    /// <summary>
    /// The physical folder that holds the help content, one sub-folder per culture (<c>en-US/general/blog.md</c>).
    /// Required.
    /// </summary>
    public string? ContentRoot { get; set; }

    /// <summary>
    /// The address the content is served below, <c>/help</c> by default. It has to match
    /// <c>HelpOptions.ContentBasePath</c> of the client, which asks for <c>{RequestPath}/{culture}/{path}</c>.
    /// </summary>
    public string RequestPath { get; set; } = "/help";

    /// <summary>
    /// The name of an authorization policy the signed-in user has to satisfy in addition to being signed in; a
    /// user who does not gets 403. Null requires only a signed-in user.
    /// </summary>
    public string? PolicyName { get; set; }

    /// <summary>A comma-separated list of roles of which the signed-in user has to have at least one; null or empty allows every role.</summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Names of folders, at any depth below the culture folder, that are never served — build output a documentation
    /// source folder usually holds. Folders starting with <c>.</c> are never served either.
    /// </summary>
    public ICollection<string> ExcludedFolders { get; } = new List<string> { "_site", "obj", "bin", "node_modules" };
}
