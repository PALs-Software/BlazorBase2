namespace BlazorBase.Components.Help;

/// <summary>
/// Maps URL paths to help topics and back, from a list of route templates and the topic each one belongs to.
/// </summary>
/// <remarks>
/// Literal segments match case-insensitively; <c>{name}</c>, <c>{name:constraint}</c> and optional parameters
/// match any single segment (or none, when optional) and <c>{*name}</c> matches the rest of the path. When
/// several templates match, the one with the most literal segments wins, then the one with the fewest
/// parameters, then the one listed first.
/// </remarks>
public sealed class HelpRouteMap
{
    private readonly List<Route> Routes = [];

    /// <summary>Creates the map from <paramref name="routes"/>, pairs of a route template and its topic.</summary>
    public HelpRouteMap(IEnumerable<KeyValuePair<string, string>> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        foreach (var (template, topic) in routes)
            Routes.Add(Route.Parse(template, topic));
    }

    /// <summary>Returns the topic of the route that matches <paramref name="path"/> best, or null.</summary>
    /// <param name="path">The path relative to the application's base address; a query string or fragment is ignored.</param>
    public string? ResolveTopic(string? path)
    {
        var segments = SplitPath(path);
        Route? best = null;

        foreach (var route in Routes)
            if (route.Matches(segments) && (best is null || route.IsMoreSpecificThan(best)))
                best = route;

        return best?.Topic;
    }

    /// <summary>
    /// Returns the path of a route of <paramref name="topic"/> that needs no parameters, with a leading
    /// <c>/</c>, or null when the topic has none.
    /// </summary>
    public string? GetRoute(string topic)
    {
        string? shortest = null;

        foreach (var route in Routes)
        {
            if (!string.Equals(route.Topic, topic, StringComparison.OrdinalIgnoreCase) || route.StaticPath is not { } path)
                continue;

            if (shortest is null || path.Length < shortest.Length)
                shortest = path;
        }

        return shortest;
    }

    private static string[] SplitPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return [];

        var end = path.IndexOfAny(['?', '#']);
        var clean = end >= 0 ? path[..end] : path;

        return clean.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
    }

    private sealed class Route
    {
        private readonly Segment[] Segments;

        private Route(string topic, Segment[] segments)
        {
            Topic = topic;
            Segments = segments;
        }

        public string Topic { get; }

        public int LiteralCount => Segments.Count(segment => segment.Kind == SegmentKind.Literal);

        public int ParameterCount => Segments.Length - LiteralCount;

        public string? StaticPath
        {
            get
            {
                var literals = Segments.TakeWhile(segment => segment.Kind == SegmentKind.Literal).ToArray();

                if (Segments.Skip(literals.Length).Any(segment => segment.Kind != SegmentKind.Optional))
                    return null;

                return "/" + string.Join('/', literals.Select(segment => segment.Text));
            }
        }

        public static Route Parse(string template, string topic)
        {
            var segments = (template ?? string.Empty)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Segment.Parse)
                .ToArray();

            return new Route(topic, segments);
        }

        public bool IsMoreSpecificThan(Route other) =>
            LiteralCount != other.LiteralCount ? LiteralCount > other.LiteralCount : ParameterCount < other.ParameterCount;

        public bool Matches(string[] path)
        {
            for (var index = 0; index < Segments.Length; index++)
            {
                var segment = Segments[index];

                if (segment.Kind == SegmentKind.CatchAll)
                    return path.Length >= index;

                if (index >= path.Length)
                    return Segments.Skip(index).All(rest => rest.Kind is SegmentKind.Optional or SegmentKind.CatchAll);

                if (segment.Kind == SegmentKind.Literal && !string.Equals(segment.Text, path[index], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return path.Length == Segments.Length;
        }
    }

    private readonly record struct Segment(SegmentKind Kind, string Text)
    {
        public static Segment Parse(string text)
        {
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
                return new Segment(SegmentKind.Literal, text);

            var inner = text[1..^1];

            if (inner.StartsWith('*'))
                return new Segment(SegmentKind.CatchAll, inner.TrimStart('*'));

            return new Segment(inner.EndsWith('?') ? SegmentKind.Optional : SegmentKind.Parameter, inner);
        }
    }

    private enum SegmentKind
    {
        Literal,
        Parameter,
        Optional,
        CatchAll,
    }
}
