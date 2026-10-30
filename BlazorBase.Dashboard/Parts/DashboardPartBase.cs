using Microsoft.AspNetCore.Components;

namespace BlazorBase.Dashboard;

/// <summary>
/// Optional base class for parts that load data: shows the loading skeleton while <see cref="LoadAsync"/> runs for the first
/// time, reloads silently on <see cref="RefreshAsync"/> and hands an exception to the retry card of the role center.
/// </summary>
public abstract class DashboardPartBase : ComponentBase, IDashboardRefreshable, IDisposable
{
    private readonly CancellationTokenSource DisposalSource = new();

    /// <summary>
    /// Initializes the base class.
    /// </summary>
    protected DashboardPartBase() => DisposalToken = DisposalSource.Token;

    /// <summary>
    /// The frame the role center put around this part; null when the part is rendered outside a role center.
    /// </summary>
    [CascadingParameter]
    public IDashboardPartHost? Host { get; set; }

    /// <summary>
    /// Cancelled when the part is removed from the page; pass it to every data call.
    /// </summary>
    protected CancellationToken DisposalToken { get; }

    /// <summary>
    /// Loads the part's data. Runs once on initialization and again for every refresh. Call <see cref="ReportEmpty"/> from
    /// here to collapse the part when there is nothing to show.
    /// </summary>
    protected abstract Task LoadAsync();

    /// <summary>
    /// Collapses the part (true) or shows it again (false); see <see cref="IDashboardPartHost.ReportEmpty"/>.
    /// </summary>
    protected void ReportEmpty(bool isEmpty = true) => Host?.ReportEmpty(isEmpty);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Host?.ReportLoading();

        try
        {
            await LoadAsync();
        }
        finally
        {
            Host?.ReportLoaded();
        }
    }

    /// <inheritdoc />
    public virtual async Task RefreshAsync()
    {
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposalSource.Cancel();
        DisposalSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
