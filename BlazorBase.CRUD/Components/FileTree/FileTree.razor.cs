using BlazorBase.CRUD.Models.FileTree;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components.FileTree;

/// <summary>
/// Renders a navigable file tree from a list of <see cref="FileTreeNode"/> roots.
/// Supports single-node selection, expand/collapse, and WAI-ARIA tree keyboard navigation:
/// ArrowDown/ArrowUp move focus between visible nodes; ArrowRight expands a collapsed directory
/// or moves focus to the first child when already expanded; ArrowLeft collapses an expanded
/// directory or moves focus to the parent node; Enter/Space select a file or toggle a directory.
/// </summary>
public partial class FileTree : ComponentBase
{
    /// <summary>Root nodes to display. Defaults to an empty list.</summary>
    [Parameter]
    public IReadOnlyList<FileTreeNode> Nodes { get; set; } = [];

    /// <summary>Currently selected file path (two-way bindable).</summary>
    [Parameter]
    public string? SelectedPath { get; set; }

    /// <summary>Raised when a node is selected, passing the node's path.</summary>
    [Parameter]
    public EventCallback<string> SelectedPathChanged { get; set; }

    /// <summary>Raised when the user selects a file node.</summary>
    [Parameter]
    public EventCallback<FileTreeNode> OnNodeSelected { get; set; }

    /// <summary>When true root-level nodes start expanded. Defaults to true.</summary>
    [Parameter]
    public bool ExpandRootByDefault { get; set; } = true;

    /// <summary>Text shown when <see cref="Nodes"/> is empty.</summary>
    [Parameter]
    public string EmptyLabel { get; set; } = "No files";

    private string? FocusedPath { get; set; }

    private readonly Dictionary<string, ElementReference> NodeElements = [];

    private readonly Dictionary<string, bool> ExpansionState = [];

    private HashSet<string> RootPaths { get; set; } = [];

    protected override void OnParametersSet()
    {
        RootPaths = Nodes.Select(n => n.Path).ToHashSet();

        if (FocusedPath is null && Nodes.Count > 0)
            FocusedPath = Nodes[0].Path;
    }

    private bool IsNodeExpanded(string path)
    {
        if (ExpansionState.TryGetValue(path, out var state))
            return state;

        return RootPaths.Contains(path) && ExpandRootByDefault;
    }

    private List<FileTreeNode> BuildVisibleNodes()
    {
        var result = new List<FileTreeNode>();

        foreach (var node in Nodes)
            CollectVisible(node, result);

        return result;
    }

    private void CollectVisible(FileTreeNode node, List<FileTreeNode> result)
    {
        result.Add(node);

        if (!node.IsDirectory || !IsNodeExpanded(node.Path))
            return;

        foreach (var child in node.Children)
            CollectVisible(child, result);
    }

    internal async Task OnNodeActivated(FileTreeNode node)
    {
        SelectedPath = node.Path;
        FocusedPath = node.Path;

        if (SelectedPathChanged.HasDelegate)
            await SelectedPathChanged.InvokeAsync(node.Path);

        if (OnNodeSelected.HasDelegate)
            await OnNodeSelected.InvokeAsync(node);
    }

    internal void OnNodeElementReady((string Path, ElementReference Element) info)
    {
        NodeElements[info.Path] = info.Element;
    }

    internal void OnExpansionChanged(string path, bool expanded)
    {
        ExpansionState[path] = expanded;
    }

    internal async Task OnKeyNavigationAsync((string Key, FileTreeNode Node, bool IsExpanded) args)
    {
        var visible = BuildVisibleNodes();
        var currentIndex = visible.FindIndex(n => n.Path == args.Node.Path);

        if (args.Key == "ArrowDown")
        {
            if (currentIndex < 0 || currentIndex >= visible.Count - 1)
                return;

            FocusedPath = visible[currentIndex + 1].Path;
        }
        else if (args.Key == "ArrowUp")
        {
            if (currentIndex <= 0)
                return;

            FocusedPath = visible[currentIndex - 1].Path;
        }
        else if (args.Key == "ArrowRight" && args.Node.IsDirectory && args.IsExpanded)
        {
            if (args.Node.Children.Count > 0)
                FocusedPath = args.Node.Children[0].Path;
        }
        else if (args.Key == "ArrowLeft")
        {
            var parent = FindParent(Nodes, args.Node.Path);

            if (parent is not null)
                FocusedPath = parent.Path;
        }

        StateHasChanged();

        if (FocusedPath is not null && NodeElements.TryGetValue(FocusedPath, out var element))
        {
            try { await element.FocusAsync(); }
            catch { }
        }
    }

    private static FileTreeNode? FindParent(IReadOnlyList<FileTreeNode> nodes, string childPath)
    {
        foreach (var node in nodes)
        {
            if (!node.IsDirectory)
                continue;

            if (node.Children.Any(c => c.Path == childPath))
                return node;

            var found = FindParent(node.Children, childPath);

            if (found is not null)
                return found;
        }

        return null;
    }
}
