namespace BlazorBase.Components.Help;

/// <summary>The headings and the field entries of a help document, as <see cref="HelpStructureReader"/> reads them.</summary>
/// <param name="Headings">Every heading of the body, in document order, with its id.</param>
/// <param name="Fields">The entries of the fields section, in document order; empty without a <c>&lt;!-- help:fields --&gt;</c> marker.</param>
public sealed record HelpDocumentStructure(IReadOnlyList<HelpHeading> Headings, IReadOnlyList<HelpFieldEntry> Fields)
{
    /// <summary>The smallest number of table-of-contents headings that makes a table of contents worth showing.</summary>
    public const int MinimumTocHeadings = 2;

    /// <summary>A document without headings and fields.</summary>
    public static HelpDocumentStructure Empty { get; } = new([], []);

    /// <summary>The level-2 and level-3 headings, nested: a level-3 heading belongs to the level-2 heading above it.</summary>
    public IReadOnlyList<HelpTocEntry> TableOfContents => BuildTableOfContents();

    /// <summary>True when the table of contents has at least <see cref="MinimumTocHeadings"/> headings.</summary>
    public bool HasTableOfContents => Headings.Count(heading => heading.Level is 2 or 3) >= MinimumTocHeadings;

    private List<HelpTocEntry> BuildTableOfContents()
    {
        var entries = new List<HelpTocEntry>();
        HelpHeading? parent = null;
        var children = new List<HelpHeading>();

        foreach (var heading in Headings.Where(heading => heading.Level is 2 or 3))
        {
            if (heading.Level == 3 && parent is { Level: 2 })
            {
                children.Add(heading);
                continue;
            }

            if (parent is not null)
                entries.Add(new HelpTocEntry(parent, children));

            parent = heading;
            children = [];
        }

        if (parent is not null)
            entries.Add(new HelpTocEntry(parent, children));

        return entries;
    }
}
