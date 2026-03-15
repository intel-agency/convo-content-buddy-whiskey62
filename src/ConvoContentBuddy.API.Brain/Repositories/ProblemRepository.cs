using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Entities;
using ConvoContentBuddy.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace ConvoContentBuddy.API.Brain.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProblemRepository"/> for the API layer.
/// Seeder-oriented methods are out of scope and throw <see cref="NotImplementedException"/>.
/// </summary>
public class ProblemRepository : IProblemRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="ProblemRepository"/>.</summary>
    /// <param name="context">The EF Core database context.</param>
    public ProblemRepository(AppDbContext context) => _context = context;

    /// <inheritdoc/>
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _context.Problems.CountAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Problem>> SearchByVectorAsync(
        Vector queryVector,
        string modelName,
        int dimensions,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Two-step approach: first obtain ordered IDs via pgvector cosine ANN search,
        // then load full entities with navigation properties by those IDs.
        var orderedIds = await _context.Database
            .SqlQuery<Guid>($"""
                SELECT id FROM app.problems
                WHERE embedding_model = {modelName}
                  AND embedding_dimensions = {dimensions}
                  AND embedding IS NOT NULL
                ORDER BY embedding <=> {queryVector}
                LIMIT {limit}
                """)
            .ToListAsync(cancellationToken);

        if (orderedIds.Count == 0)
            return [];

        var problems = await _context.Problems
            .Include(p => p.ProblemTags)
            .ThenInclude(pt => pt.Tag)
            .Where(p => orderedIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Re-sort to preserve similarity order from the vector search.
        return problems
            .OrderBy(p => orderedIds.IndexOf(p.Id))
            .ToList();
    }

    /// <inheritdoc/>
    public Task<Problem> UpsertBySlugAsync(Problem problem, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("UpsertBySlugAsync is in seeder scope and is not implemented in the API layer.");

    /// <inheritdoc/>
    public Task<IReadOnlyList<Problem>> GetUnembeddedForProfileAsync(
        string modelName,
        int dimensions,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("GetUnembeddedForProfileAsync is in seeder scope and is not implemented in the API layer.");

    /// <inheritdoc/>
    public Task UpdateEmbeddingAsync(
        Guid problemId,
        Vector embedding,
        string modelName,
        int dimensions,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("UpdateEmbeddingAsync is in seeder scope and is not implemented in the API layer.");
}
