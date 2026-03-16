using ConvoContentBuddy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProblemTagRepository"/> using <see cref="AppDbContext"/>.
/// </summary>
public class ProblemTagRepository : IProblemTagRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="ProblemTagRepository"/>.</summary>
    /// <param name="context">The EF Core database context.</param>
    public ProblemTagRepository(AppDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task SyncTagsAsync(
        Guid problemId,
        IEnumerable<Guid> tagIds,
        CancellationToken cancellationToken = default)
    {
        var desiredIds = tagIds.ToHashSet();

        var existing = await _context.ProblemTags
            .Where(pt => pt.ProblemId == problemId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(pt => pt.TagId).ToHashSet();

        var toAdd = desiredIds
            .Where(id => !existingIds.Contains(id))
            .Select(id => new ProblemTag { ProblemId = problemId, TagId = id })
            .ToList();

        var toRemove = existing
            .Where(pt => !desiredIds.Contains(pt.TagId))
            .ToList();

        if (toAdd.Count > 0)
            _context.ProblemTags.AddRange(toAdd);

        if (toRemove.Count > 0)
            _context.ProblemTags.RemoveRange(toRemove);

        if (toAdd.Count > 0 || toRemove.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Tag>> GetTagsForProblemAsync(
        Guid problemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProblemTags
            .Include(pt => pt.Tag)
            .Where(pt => pt.ProblemId == problemId)
            .Select(pt => pt.Tag)
            .ToListAsync(cancellationToken);
    }
}
