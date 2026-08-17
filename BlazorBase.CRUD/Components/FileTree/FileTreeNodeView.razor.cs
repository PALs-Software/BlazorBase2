using BlazorBase.CRUD.Models.FileTree;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorBase.CRUD.Components.FileTree;

/// <summary>
/// Renders a single node in the file tree, recursing into child nodes when expanded.
/// Supports click to select/toggle; Enter/Space to select/toggle; ArrowRight to expand a
/// collapsed directory (or move focus to first child when already expanded); ArrowLeft to
/// collapse an expanded directory (or move focus to the parent); ArrowUp/ArrowDown move
/// focus to the previous/next visible node via the roving-tabindex owned by the parent
/// <see cref="FileTree"/>.
/// </summary>
public partial class FileTreeNodeView : ComponentBase
{
    /// <summary>The node to render.</summary>
    [Parameter]
    public FileTreeNode Node { get; set; } = default!;

    /// <summary>Currently selected path — used to drive <c>aria-selected</c> and highlight.</summary>
    [Parameter]
    public string? SelectedPath { get; set; }

    /// <summary>Whether this node starts expanded. Applied only on first render.</summary>
    [Parameter]
    public bool ExpandByDefault { get; set; }

    /// <summary>Bubbles a selected node up to the <see cref="FileTree"/> root.</summary>
    [Parameter]
    public EventCallback<FileTreeNode> OnNodeSelected { get; set; }

    /// <summary>
    /// The path of the node that currently holds keyboard focus in the roving-tabindex scheme.
    /// This node sets <c>tabindex="0"</c> when its <see cref="Node.Path"/> matches; all others use <c>-1</c>.
    /// </summary>
    [Parameter]
    public string? FocusedPath { get; set; }

    /// <summary>
    /// Invoked by the parent <see cref="FileTree"/> mechanism to request that a keyboard-navigation
    /// action (ArrowUp/ArrowDown/ArrowLeft/ArrowRight) be forwarded upward.  The string argument is
    /// the key name (e.g. "ArrowDown").
    /// </summary>
    [Parameter]
    public EventCallback<(string Key, FileTreeNode Node, bool IsExpanded)> OnKeyNavigation { get; set; }

    /// <summary>Raised after this component renders so the parent can obtain the row <see cref="ElementReference"/>.</summary>
    [Parameter]
    public EventCallback<(string Path, ElementReference Element)> OnElementReady { get; set; }

    /// <summary>Raised when this directory node's expanded state changes.</summary>
    [Parameter]
    public EventCallback<(string Path, bool Expanded)> OnExpansionChanged { get; set; }

    internal bool IsExpanded { get; private set; }
    private bool IsSelected => SelectedPath == Node.Path;
    private bool IsFocused => FocusedPath == Node.Path;

    internal ElementReference RowElement;

    protected override void OnInitialized()
    {
        IsExpanded = ExpandByDefault;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        if (OnElementReady.HasDelegate)
            await OnElementReady.InvokeAsync((Node.Path, RowElement));
    }

    private async Task SetExpandedAsync(bool expanded)
    {
        IsExpanded = expanded;

        if (OnExpansionChanged.HasDelegate)
            await OnExpansionChanged.InvokeAsync((Node.Path, expanded));
    }

    private async Task HandleClickAsync()
    {
        if (Node.IsDirectory)
        {
            await SetExpandedAsync(!IsExpanded);
            return;
        }

        if (OnNodeSelected.HasDelegate)
            await OnNodeSelected.InvokeAsync(Node);
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Enter":
            case " ":
                await HandleClickAsync();
                break;

            case "ArrowRight":
                if (Node.IsDirectory && !IsExpanded)
                    await SetExpandedAsync(true);
                else if (OnKeyNavigation.HasDelegate)
                    await OnKeyNavigation.InvokeAsync((args.Key, Node, IsExpanded));
                break;

            case "ArrowLeft":
                if (Node.IsDirectory && IsExpanded)
                    await SetExpandedAsync(false);
                else if (OnKeyNavigation.HasDelegate)
                    await OnKeyNavigation.InvokeAsync((args.Key, Node, IsExpanded));
                break;

            case "ArrowDown":
            case "ArrowUp":
                if (OnKeyNavigation.HasDelegate)
                    await OnKeyNavigation.InvokeAsync((args.Key, Node, IsExpanded));
                break;
        }
    }
}
