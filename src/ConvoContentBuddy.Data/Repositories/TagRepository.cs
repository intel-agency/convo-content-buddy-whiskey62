using ConvoContentBuddy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITagRepository"/> using <see cref="AppDbContext"/>.
/// </summary>
public class TagRepository : ITagRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="TagRepository"/>.</summary>
    /// <param name="context">The EF Core database context.</param>
    public TagRepository(AppDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Tag>> UpsertTagsAsync(
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken = default)
    {
        var names = tagNames.ToList();
        if (names.Count == 0)
            return [];

        var existing = await _context.Tags
            .Where(t => names.Contains(t.Name))
            .ToListAsync(cancellationToken);

        var existingNames = existing.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        var newTags = names
            .Where(n => !existingNames.Contains(n))
            .Select(n => new Tag { Name = n })
            .ToList();

        if (newTags.Count > 0)
        {
            _context.Tags.AddRange(newTags);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return [.. existing, .. newTags];
    }

    /// <inheritdoc/>
    public async Task<Tag> GetOrCreateByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var tag = await _context.Tags
            .FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

        if (tag is not null)
            return tag;

        tag = new Tag { Name = name };
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync(cancellationToken);
        return tag;
    }
}
