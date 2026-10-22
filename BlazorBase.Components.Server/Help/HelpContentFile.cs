namespace BlazorBase.Components.Server.Help;

/// <summary>A file of the help content that may be served.</summary>
/// <param name="PhysicalPath">The full path of the file.</param>
/// <param name="ContentType">The media type to serve it with; <c>text/markdown; charset=utf-8</c> for Markdown.</param>
/// <param name="IsMarkdown">Whether the file is Markdown, which is read and filtered rather than streamed.</param>
public sealed record HelpContentFile(string PhysicalPath, string ContentType, bool IsMarkdown);
