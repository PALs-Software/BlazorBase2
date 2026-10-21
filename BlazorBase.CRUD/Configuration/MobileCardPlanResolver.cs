using System.Reflection;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Decides which columns a list's tile shows and the part each plays.
/// </summary>
/// <remarks>
/// <para>Sources, strongest first: the list's <see cref="MobileCardConfiguration{TModel}"/>, the
/// <see cref="PropertyColumnConfig{TModel}.MobileRole"/> of a column, the <see cref="MobileVisibleAttribute"/> on the
/// entity property.</para>
/// <para>When any source declares a title, subtitle, field or badge the tile shows exactly those (plus the action
/// columns) and ignores undecided columns. When none does, the choice is automatic: the title is the first text
/// column, the subtitle the second, up to two boolean or enum columns become badges, and up to
/// <see cref="DefaultMaxFields"/> more columns follow, text before dates before numbers. Raw identifiers, binary
/// data, collections and untemplated objects are never chosen.</para>
/// </remarks>
public static class MobileCardPlanResolver
{
    /// <summary>The container width in pixels below which a list shows tiles unless told otherwise.</summary>
    public const int DefaultBreakpoint = 700;

    /// <summary>The most label and value pairs the automatic choice puts on a tile.</summary>
    public const int DefaultMaxFields = 4;

    private const int MaxAutoBadges = 2;

    public static MobileCardPlan<TModel> Resolve<TModel>(
        IReadOnlyList<PropertyColumnConfig<TModel>> columns,
        MobileCardConfiguration<TModel>? configuration,
        Func<PropertyColumnConfig<TModel>, bool>? isAllowed = null)
    {
        var candidates = BuildCandidates(columns, configuration, isAllowed, out var extraColumns);
        var actionColumns = Ordered(candidates.Where(candidate => candidate.Role == MobileRole.Actions)).Select(candidate => candidate.Column).ToList();
        var isExplicit = candidates.Any(candidate => candidate.Role is MobileRole.Title or MobileRole.Subtitle or MobileRole.Field or MobileRole.Badge);

        var selection = isExplicit ? SelectExplicit(candidates, configuration) : SelectAutomatic(candidates, configuration);

        return new MobileCardPlan<TModel>
        {
            Title = selection.Title?.Column,
            Subtitle = selection.Subtitle?.Column,
            Fields = [.. selection.Fields.Select(candidate => candidate.Column)],
            Badges = [.. selection.Badges.Select(candidate => candidate.Column)],
            ActionColumns = actionColumns,
            ExtraColumns = extraColumns,
            Labels = candidates
                .Where(candidate => candidate.Label is not null)
                .ToDictionary(candidate => candidate.Column, candidate => candidate.Label!)
        };
    }

    /// <summary>
    /// The kind of value a column holds for the tile's purposes.
    /// </summary>
    public static MobileValueKind Classify<TModel>(PropertyColumnConfig<TModel> column)
    {
        var type = PropertyTypeOf(column);
        var hasTemplate = column.Template is not null;

        if (type == typeof(string))
            return MobileValueKind.Text;

        if (type == typeof(bool))
            return MobileValueKind.Flag;

        if (type.IsEnum)
            return MobileValueKind.Enum;

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan))
            return MobileValueKind.Date;

        if (IsNumber(type))
            return IsIdentifierName(column.PropertyName) && !hasTemplate ? MobileValueKind.Excluded : MobileValueKind.Number;

        return hasTemplate ? MobileValueKind.Other : MobileValueKind.Excluded;
    }

    /// <summary>
    /// Whether the column is a row-action column of the kind the list pages declare: bound to the primary key only
    /// to be a column, not sortable, drawing buttons through its template.
    /// </summary>
    public static bool LooksLikeActionColumn<TModel>(PropertyColumnConfig<TModel> column)
        => column.Template is not null && !column.Sortable && LastSegment(column.PropertyName) == "Id";

    private static List<Candidate<TModel>> BuildCandidates<TModel>(
        IReadOnlyList<PropertyColumnConfig<TModel>> columns,
        MobileCardConfiguration<TModel>? configuration,
        Func<PropertyColumnConfig<TModel>, bool>? isAllowed,
        out List<PropertyColumnConfig<TModel>> extraColumns)
    {
        var entries = configuration?.Fields ?? [];
        var claimed = new HashSet<MobileFieldConfig<TModel>>();
        var candidates = new List<Candidate<TModel>>();
        extraColumns = [];

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            candidates.Add(CreateCandidate(column, index, FindEntry(entries, claimed, column)));
        }

        foreach (var entry in entries.Where(entry => !claimed.Contains(entry)))
        {
            var column = new PropertyColumnConfig<TModel> { Property = entry.Property };
            column.ResolvePropertyName();

            if (isAllowed is not null && !isAllowed(column))
                continue;

            extraColumns.Add(column);
            candidates.Add(CreateCandidate(column, columns.Count + extraColumns.Count, entry));
        }

        return candidates;
    }

    private static MobileFieldConfig<TModel>? FindEntry<TModel>(
        List<MobileFieldConfig<TModel>> entries,
        HashSet<MobileFieldConfig<TModel>> claimed,
        PropertyColumnConfig<TModel> column)
    {
        if (column.MobileRole == MobileRole.Actions || LooksLikeActionColumn(column))
            return null;

        var entry = entries.FirstOrDefault(candidate => !claimed.Contains(candidate) && candidate.PropertyName == column.PropertyName);

        if (entry is not null)
            claimed.Add(entry);

        return entry;
    }

    private static Candidate<TModel> CreateCandidate<TModel>(PropertyColumnConfig<TModel> column, int index, MobileFieldConfig<TModel>? entry)
    {
        var attribute = ExpressionHelper.GetPropertyInfo(column.Property).GetCustomAttribute<MobileVisibleAttribute>(inherit: true);
        var role = ResolveRole(column, entry, attribute);
        var priority = entry?.Priority ?? column.MobilePriority ?? attribute?.Order ?? MobileVisibleAttribute.Unordered;

        return new Candidate<TModel>(column, index, role, priority, Classify(column), entry?.Label);
    }

    private static MobileRole ResolveRole<TModel>(PropertyColumnConfig<TModel> column, MobileFieldConfig<TModel>? entry, MobileVisibleAttribute? attribute)
    {
        if (entry is not null)
            return entry.Role;

        if (column.MobileRole != MobileRole.Auto)
            return column.MobileRole;

        if (attribute is not null)
            return attribute.Role;

        return LooksLikeActionColumn(column) ? MobileRole.Actions : MobileRole.Auto;
    }

    private static Selection<TModel> SelectExplicit<TModel>(List<Candidate<TModel>> candidates, MobileCardConfiguration<TModel>? configuration)
    {
        var titles = Ordered(candidates.Where(candidate => candidate.Role == MobileRole.Title)).ToList();
        var subtitles = Ordered(candidates.Where(candidate => candidate.Role == MobileRole.Subtitle)).ToList();
        var title = titles.FirstOrDefault() ?? FirstAutomaticText(candidates);

        var fields = Ordered(candidates
            .Where(candidate => candidate.Role == MobileRole.Field)
            .Concat(titles.Skip(1))
            .Concat(subtitles.Skip(1))).ToList();

        var capped = configuration?.MaxFields is { } maxFields ? fields.Take(Math.Max(maxFields, 0)).ToList() : fields;

        return new Selection<TModel>(
            title,
            subtitles.FirstOrDefault(),
            capped,
            Ordered(candidates.Where(candidate => candidate.Role == MobileRole.Badge)).ToList());
    }

    private static Candidate<TModel>? FirstAutomaticText<TModel>(List<Candidate<TModel>> candidates)
        => candidates.FirstOrDefault(candidate => candidate.Role == MobileRole.Auto && candidate.Kind == MobileValueKind.Text);

    private static Selection<TModel> SelectAutomatic<TModel>(List<Candidate<TModel>> candidates, MobileCardConfiguration<TModel>? configuration)
    {
        var pool = Ordered(candidates.Where(candidate => candidate.Role == MobileRole.Auto && candidate.Kind != MobileValueKind.Excluded)).ToList();
        var texts = pool.Where(candidate => candidate.Kind == MobileValueKind.Text).Take(2).ToList();
        var title = texts.ElementAtOrDefault(0);
        var subtitle = texts.ElementAtOrDefault(1);

        var badges = pool
            .Where(candidate => candidate.Kind is MobileValueKind.Flag or MobileValueKind.Enum)
            .Take(MaxAutoBadges)
            .ToList();

        var fields = pool
            .Where(candidate => candidate != title && candidate != subtitle && !badges.Contains(candidate))
            .OrderBy(candidate => Rank(candidate.Kind))
            .ThenBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Index)
            .Take(Math.Max(configuration?.MaxFields ?? DefaultMaxFields, 0))
            .ToList();

        return new Selection<TModel>(title, subtitle, fields, badges);
    }

    private static int Rank(MobileValueKind kind) => kind switch
    {
        MobileValueKind.Text => 0,
        MobileValueKind.Date => 1,
        MobileValueKind.Number => 2,
        MobileValueKind.Other => 3,
        _ => 4
    };

    private static IEnumerable<Candidate<TModel>> Ordered<TModel>(IEnumerable<Candidate<TModel>> candidates)
        => candidates.OrderBy(candidate => candidate.Priority).ThenBy(candidate => candidate.Index);

    private static Type PropertyTypeOf<TModel>(PropertyColumnConfig<TModel> column)
    {
        var type = ExpressionHelper.GetPropertyInfo(column.Property).PropertyType;

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static bool IsNumber(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(decimal)
        || type == typeof(double) || type == typeof(float) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort);

    private static bool IsIdentifierName(string propertyName)
    {
        var name = LastSegment(propertyName);

        return name == "Id" || (name.Length > 2 && name.EndsWith("Id", StringComparison.Ordinal) && Char.IsLower(name[^3]));
    }

    private static string LastSegment(string propertyPath)
    {
        var dot = propertyPath.LastIndexOf('.');

        return dot < 0 ? propertyPath : propertyPath[(dot + 1)..];
    }

    private sealed record Candidate<TModel>(
        PropertyColumnConfig<TModel> Column,
        int Index,
        MobileRole Role,
        int Priority,
        MobileValueKind Kind,
        string? Label);

    private sealed record Selection<TModel>(
        Candidate<TModel>? Title,
        Candidate<TModel>? Subtitle,
        List<Candidate<TModel>> Fields,
        List<Candidate<TModel>> Badges);
}
