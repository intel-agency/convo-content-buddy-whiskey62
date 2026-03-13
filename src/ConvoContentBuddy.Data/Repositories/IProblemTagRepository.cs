using ConvoContentBuddy.Data.Entities;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// Repository interface for managing <see cref="ProblemTag"/> join records that associate
/// <see cref="Problem"/> entities with <see cref="Tag"/> entities.
/// </summary>
public interface IProblemTagRepository
{
    /// <summary>
    /// Synchronizes the tag associations for the specified problem so that the set of linked
    /// tags exactly matches <paramref name="tagIds"/>. Associations that do not exist in the
    /// store are inserted; associations that are no longer present in <paramref name="tagIds"/>
    /// are removed. Tags and Problems must already exist before calling this method.
    /// </summary>
    /// <param name="problemId">The ID of the problem whose tag links are being synchronized.</param>
    /// <param name="tagIds">
    /// The complete, desired set of <see cref="Tag"/> IDs to associate with the problem.
    /// Passing an empty collection removes all existing associations.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SyncTagsAsync(
        Guid problemId,
        IEnumerable<Guid> tagIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all <see cref="Tag"/> entities currently linked to the specified problem.
    /// </summary>
    /// <param name="problemId">The ID of the problem to retrieve tags for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tags currently associated with the problem.</returns>
    Task<IReadOnlyList<Tag>> GetTagsForProblemAsync(
        Guid problemId,
        CancellationToken cancellationToken = default);
}
