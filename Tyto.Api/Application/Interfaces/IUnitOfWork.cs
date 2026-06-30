namespace Tyto.Api.Application.Interfaces;

/// <summary>
/// Manages database transaction boundaries and commits all pending changes atomically.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits all pending changes to the database in a single transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if there are any pending changes tracked by the context.
    /// </summary>
    /// <returns>True if there are pending changes; otherwise, false.</returns>
    bool HasChanges();
}
