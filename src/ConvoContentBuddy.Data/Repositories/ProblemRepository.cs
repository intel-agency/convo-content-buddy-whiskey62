using ConvoContentBuddy.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace ConvoContentBuddy.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProblemRepository"/> using <see cref="AppDbContext"/>.
/// Provides full read/write support for both the seeder and API layers.
/// </summary>
public class ProblemRepository : IProblemRepository
{
    private readonly AppDbContext _context;

    /// <summary>Initializes a new instance of <see cref="ProblemRepository"/>.</summary>
    /// <param name="context">The EF Core database context.</param>
    public ProblemRepository(AppDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<Problem> UpsertBySlugAsync(Problem problem, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Problems
            .FirstOrDefaultAsync(p => p.Slug == problem.Slug, cancellationToken);

        if (existing is not null)
        {
            existing.Title = problem.Title;
            existing.Difficulty = problem.Difficulty;
            existing.Description = problem.Description;
            existing.QuestionId = problem.QuestionId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            problem.SeededAt = DateTimeOffset.UtcNow;
            problem.UpdatedAt = DateTimeOffset.UtcNow;
            _context.Problems.Add(problem);
            existing = problem;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Problem>> GetUnembeddedForProfileAsync(
        string modelName,
        int dimensions,
        CancellationToken cancellationToken = default)
    {
        var results = await _context.Problems
            .Where(p =>
                p.Embedding == null ||
                p.EmbeddingModel != modelName ||
                p.EmbeddingDimensions != dimensions)
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <inheritdoc/>
    public async Task UpdateEmbeddingAsync(
        Guid problemId,
        Vector embedding,
        string modelName,
        int dimensions,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default)
    {
        var problem = await _context.Problems
            .FirstOrDefaultAsync(p => p.Id == problemId, cancellationToken);

        if (problem is null)
            return;

        problem.Embedding = embedding;
        problem.EmbeddingModel = modelName;
        problem.EmbeddingDimensions = dimensions;
        problem.EmbeddingGeneratedAt = generatedAt;
        problem.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

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
}
