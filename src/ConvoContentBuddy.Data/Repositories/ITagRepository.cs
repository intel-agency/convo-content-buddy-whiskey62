using ConvoContentBuddy.Data.Entities;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// Repository interface for <see cref="Tag"/> persistence operations.
/// </summary>
public interface ITagRepository
{
    /// <summary>
    /// Creates any tags in <paramref name="tagNames"/> that do not already exist,
    /// then returns all <see cref="Tag"/> records matching those names.
    /// </summary>
    /// <param name="tagNames">Collection of tag names to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All <see cref="Tag"/> entities matching the supplied names.</returns>
    Task<IReadOnlyList<Tag>> UpsertTagsAsync(
        IEnumerable<string> tagNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <see cref="Tag"/> with the given name, creating it if it does not exist.
    /// </summary>
    /// <param name="name">Tag name to look up or create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Tag> GetOrCreateByNameAsync(string name, CancellationToken cancellationToken = default);
}
