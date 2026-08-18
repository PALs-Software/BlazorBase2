namespace BlazorBase.Components.Models.FileTree;

/// <summary>
/// A single node in a generic file tree; may represent a directory or a file.
/// </summary>
public sealed class FileTreeNode
{
    /// <summary>Display name of this node (last path segment).</summary>
    public required string Name { get; init; }

    /// <summary>Full '/'-separated path from the tree root.</summary>
    public required string Path { get; init; }

    /// <summary>True when this node represents a directory; false for a file.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Ordered child nodes (directories first, then files, each sorted by name).</summary>
    public IReadOnlyList<FileTreeNode> Children { get; init; } = [];
}
