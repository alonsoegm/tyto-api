using Tyto.Api.Application.Interfaces;

namespace Tyto.Api.Infrastructure.Data;

/// <summary>
/// Implementation of the Unit of Work pattern that manages database transaction boundaries.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly TytoDbContext _context;

    public UnitOfWork(TytoDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public bool HasChanges()
    {
        return _context.ChangeTracker.HasChanges();
    }
}
