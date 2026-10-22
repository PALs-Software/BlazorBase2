using System.Reflection;

namespace BlazorBase.Components.Help;

/// <summary>Settings of the contextual help, configured through <see cref="HelpServiceCollectionExtensions.AddBlazorBaseHelp"/>.</summary>
public sealed class HelpOptions
{
    /// <summary>
    /// The address of the full documentation site, e.g. <c>https://docs.example.com/</c>. A topic is appended with
    /// the <c>.html</c> extension; <c>{culture}</c> in the address is replaced by the culture folder of the
    /// content that is shown. Null or empty hides the "Open full documentation" link and turns links to other files
    /// than topics (for example PDFs) into plain text.
    /// </summary>
    public string? DocumentationBaseUrl { get; set; }

    /// <summary>
    /// Where the help content is served from, relative to the application's base address. Serve it to signed-in users
    /// only (<c>MapBlazorBaseHelpContent</c> of <c>BlazorBase.Components.Server</c>), never as anonymous static web assets.
    /// </summary>
    public string ContentBasePath { get; set; } = "help";

    /// <summary>The culture folder tried when the content does not exist in the current culture.</summary>
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>The topic shown for pages without a <see cref="HelpTopicAttribute"/> and when a page's topic has no content.</summary>
    public string FallbackTopic { get; set; } = "getting-started";

    /// <summary>
    /// Maps a UI culture to the culture folder of the content, e.g. <c>en</c> to <c>en-US</c>. A culture without
    /// an entry is looked up by its full name, then by its language, and finally used as the folder name itself.
    /// </summary>
    public IDictionary<string, string> CultureFolders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The assemblies scanned for routable components carrying a <see cref="HelpTopicAttribute"/>.</summary>
    public IList<Assembly> Assemblies { get; } = [];

    /// <summary>Returns the folder name of the content for <paramref name="culture"/>.</summary>
    public string ResolveCultureFolder(string culture)
    {
        if (CultureFolders.TryGetValue(culture, out var folder))
            return folder;

        var separator = culture.IndexOf('-');
        var language = separator > 0 ? culture[..separator] : culture;

        return CultureFolders.TryGetValue(language, out folder) ? folder : culture;
    }
}
