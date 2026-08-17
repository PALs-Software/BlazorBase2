namespace BlazorBase.CRUD.Models.FileTree;

/// <summary>
/// Builds a <see cref="FileTreeNode"/> hierarchy from a flat list of '/'-separated paths.
/// Directories appear before files at each level; siblings are sorted by name.
/// </summary>
public static class FileTreeBuilder
{
    /// <summary>
    /// Converts a flat collection of '/'-separated file paths into a tree of
    /// <see cref="FileTreeNode"/> objects. Intermediate directory nodes are inferred
    /// automatically from path segments.
    /// </summary>
    public static IReadOnlyList<FileTreeNode> Build(IEnumerable<string> paths)
    {
        var root = new MutableNode(string.Empty, string.Empty, isDirectory: true);

        foreach (var path in paths)
        {
            var normalised = path.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(normalised))
                continue;

            var segments = normalised.Split('/');
            InsertPath(root, segments, 0, string.Empty);
        }

        return ToReadOnly(root.Children.Values);
    }

    private static void InsertPath(MutableNode parent, string[] segments, int depth, string parentPath)
    {
        if (depth >= segments.Length)
            return;

        var name = segments[depth];
        var fullPath = parentPath.Length == 0 ? name : $"{parentPath}/{name}";
        var isLast = depth == segments.Length - 1;

        if (!parent.Children.TryGetValue(name, out var node))
        {
            node = new MutableNode(name, fullPath, isDirectory: !isLast);
            parent.Children[name] = node;
        }
        else if (!isLast)
        {
            node.IsDirectory = true;
        }

        if (!isLast)
            InsertPath(node, segments, depth + 1, fullPath);
    }

    private static IReadOnlyList<FileTreeNode> ToReadOnly(IEnumerable<MutableNode> nodes)
    {
        var sorted = nodes
            .OrderByDescending(n => n.IsDirectory)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase);

        return sorted.Select(n => new FileTreeNode
        {
            Name = n.Name,
            Path = n.Path,
            IsDirectory = n.IsDirectory,
            Children = ToReadOnly(n.Children.Values)
        }).ToList();
    }

    private sealed class MutableNode(string name, string path, bool isDirectory)
    {
        public string Name { get; } = name;
        public string Path { get; } = path;
        public bool IsDirectory { get; set; } = isDirectory;
        public Dictionary<string, MutableNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
