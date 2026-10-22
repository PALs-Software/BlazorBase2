namespace BlazorBase.Components.Server.Test.Help;

/// <summary>A temporary help content folder with a small, fixed set of files; deleted on dispose.</summary>
internal sealed class HelpContentFolder : IDisposable
{
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    public HelpContentFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "help-content-" + Guid.NewGuid().ToString("N"));
        Write("en-US/general/blog.md", "# Blog\n\nPublic text.\n\n<!-- Internal-Start -->\nSecret internal text.\n<!-- Internal-End -->\n\nMore public text.\n");
        Write("en-US/general/unterminated.md", "Visible.\n\n<!-- Internal-Start -->\nNever delivered.\nNeither this.\n");
        Write("en-US/general/umlaut.md", "# Größe\n");
        Write("en-US/general/images/chart.png", PngBytes);
        Write("en-US/general/images/drawing.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        Write("en-US/docfx.json", "{}");
        Write("en-US/toc.yml", "- name: x");
        Write("en-US/general/notes.txt", "notes");
        Write("en-US/general/page.html", "<html></html>");
        Write("en-US/general/.hidden.md", "hidden");
        Write("en-US/_site/general/blog.md", "built");
        Write("en-US/_site/chart.png", PngBytes);
        Write("secret.md", "outside the culture folder");
        Write("de-DE/general/blog.md", "# Blog (de)\n");
    }

    public string Root { get; }

    public void Write(string relativePath, string content) => Write(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    public void Write(string relativePath, byte[] content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
