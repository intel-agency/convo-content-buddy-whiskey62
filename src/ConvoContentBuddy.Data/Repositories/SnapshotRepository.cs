using ConvoContentBuddy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISnapshotRepository"/> using <see cref="AppDbContext"/>.
/// </summary>
public class SnapshotRepository : ISnapshotRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="SnapshotRepository"/>.</summary>
    /// <param name="context">The EF Core database context.</param>
    public SnapshotRepository(AppDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task PersistSnapshotAsync(
        IngestionSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        _context.IngestionSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IngestionSnapshot?> LoadLatestAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        return await _context.IngestionSnapshots
            .Where(s => s.Source == source && s.IsLatest)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkAsLatestAsync(Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var target = await _context.IngestionSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

        if (target is null)
            return;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Clear all existing IsLatest flags for this source.
        await _context.IngestionSnapshots
            .Where(s => s.Source == target.Source)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.IsLatest, false),
                cancellationToken);

        // Mark only the target as latest.
        target.IsLatest = true;
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
