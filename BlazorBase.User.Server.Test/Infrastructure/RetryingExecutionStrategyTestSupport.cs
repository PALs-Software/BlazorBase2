using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>A failure the <see cref="RetryingTestExecutionStrategy"/> treats as transient, standing in for a transient Azure SQL error.</summary>
public sealed class TransientTestException() : Exception("Simulated transient database failure.");

/// <summary>
/// A retrying execution strategy over any provider. Like <c>SqlServerRetryingExecutionStrategy</c> it
/// reports <see cref="ExecutionStrategy.RetriesOnFailure"/>, so EF rejects a user-initiated transaction
/// that is not wrapped in <c>CreateExecutionStrategy().ExecuteAsync(...)</c>.
/// </summary>
public sealed class RetryingTestExecutionStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
{
    protected override bool ShouldRetryOn(Exception exception) => exception is TransientTestException;
}

/// <summary>Swaps the provider's default execution strategy for <see cref="RetryingTestExecutionStrategy"/>.</summary>
public sealed class RetryingTestExecutionStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
{
    public IExecutionStrategy Create() => new RetryingTestExecutionStrategy(dependencies);
}

/// <summary>Throws <see cref="TransientTestException"/> from the next <see cref="FailuresRemaining"/> save attempts.</summary>
public sealed class TransientFailureInterceptor : SaveChangesInterceptor
{
    public int FailuresRemaining { get; set; }

    public int Attempts { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        if (FailuresRemaining <= 0)
            return ValueTask.FromResult(result);

        FailuresRemaining--;
        throw new TransientTestException();
    }
}
