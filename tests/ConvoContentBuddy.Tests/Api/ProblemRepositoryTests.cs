extern alias BrainAssembly;

using BrainAssembly::ConvoContentBuddy.API.Brain.Repositories;
using ConvoContentBuddy.Data;
using ConvoContentBuddy.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ConvoContentBuddy.Tests.Api;

/// <summary>
/// Contract and behavior tests for <see cref="ProblemRepository"/>.
/// Tests that require raw SQL (e.g., pgvector cosine ordering) are covered at the LINQ-helper level
/// via <see cref="ProblemRepository.ApplyProfileFilter"/>, since the InMemory provider does not
/// support SQL execution.
/// </summary>
public class ProblemRepositoryTests
{
    private static AppDbContext CreateNpgsqlContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost", o => o.UseVector())
            .Options);

    private static AppDbContext CreateInMemoryContext(string dbName) =>
        new TestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    /// <summary>
    /// Verifies that <see cref="ProblemRepository"/> can be constructed with an
    /// <see cref="AppDbContext"/> instance.
    /// </summary>
    [Fact]
    public void ProblemRepository_CanBeConstructed_WithAppDbContext()
    {
        using var ctx = CreateNpgsqlContext();
        var repo = new ProblemRepository(ctx);
        repo.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that <see cref="ProblemRepository.CountAsync"/> returns the exact number of
    /// seeded rows, confirming it delegates to <c>Problems.CountAsync</c> correctly.
    /// </summary>
    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        using var ctx = CreateInMemoryContext(Guid.NewGuid().ToString());
        ctx.Problems.AddRange(
            new Problem { Id = Guid.NewGuid(), Slug = "a", QuestionId = 1, Title = "A", Difficulty = "Easy", Description = "A", SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Problem { Id = Guid.NewGuid(), Slug = "b", QuestionId = 2, Title = "B", Difficulty = "Easy", Description = "B", SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Problem { Id = Guid.NewGuid(), Slug = "c", QuestionId = 3, Title = "C", Difficulty = "Easy", Description = "C", SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();

        var repo = new ProblemRepository(ctx);
        var count = await repo.CountAsync();

        count.Should().Be(3);
    }

    /// <summary>
    /// Verifies that <see cref="ProblemRepository.ApplyProfileFilter"/> returns only problems whose
    /// <c>EmbeddingModel</c> and <c>EmbeddingDimensions</c> match the active profile <em>and</em>
    /// whose <c>Embedding</c> vector is non-null. Problems from a different profile or without a
    /// stored vector are excluded.
    /// </summary>
    [Fact]
    public async Task SearchByVectorAsync_FiltersByEmbeddingProfile()
    {
        using var ctx = CreateInMemoryContext(Guid.NewGuid().ToString());
        var activeModel = "gemini-embedding-001";
        var activeDimensions = 1536;

        ctx.Problems.AddRange(
            // Matches active profile, has embedding → should be returned.
            new Problem
            {
                Id = Guid.NewGuid(), Slug = "match", QuestionId = 1, Title = "Match", Difficulty = "Easy",
                Description = "match",
                EmbeddingModel = activeModel, EmbeddingDimensions = activeDimensions,
                Embedding = new Vector(new float[activeDimensions]),
                SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            },
            // Matches profile but has no embedding → must be excluded.
            new Problem
            {
                Id = Guid.NewGuid(), Slug = "no-vec", QuestionId = 2, Title = "No Vec", Difficulty = "Easy",
                Description = "no-vec",
                EmbeddingModel = activeModel, EmbeddingDimensions = activeDimensions,
                Embedding = null,
                SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            },
            // Different model → must be excluded.
            new Problem
            {
                Id = Guid.NewGuid(), Slug = "other-model", QuestionId = 3, Title = "Other Model", Difficulty = "Easy",
                Description = "other-model",
                EmbeddingModel = "other-model", EmbeddingDimensions = activeDimensions,
                Embedding = new Vector(new float[activeDimensions]),
                SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            },
            // Different dimensions → must be excluded.
            new Problem
            {
                Id = Guid.NewGuid(), Slug = "other-dims", QuestionId = 4, Title = "Other Dims", Difficulty = "Easy",
                Description = "other-dims",
                EmbeddingModel = activeModel, EmbeddingDimensions = 512,
                Embedding = new Vector(new float[512]),
                SeededAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var filtered = await ProblemRepository
            .ApplyProfileFilter(ctx.Problems, activeModel, activeDimensions)
            .ToListAsync();

        filtered.Should().HaveCount(1, because: "only the row with matching profile and non-null embedding qualifies");
        filtered[0].Slug.Should().Be("match");
    }

    /// <summary>
    /// Verifies that <see cref="ProblemRepository.UpsertBySlugAsync"/> throws
    /// <see cref="NotImplementedException"/> as it is in seeder scope.
    /// </summary>
    [Fact]
    public void UpsertBySlugAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateNpgsqlContext();
        var repo = new ProblemRepository(ctx);

        Action act = () => repo.UpsertBySlugAsync(new Problem());
        act.Should().Throw<NotImplementedException>();
    }

    /// <summary>
    /// Verifies that <see cref="ProblemRepository.GetUnembeddedForProfileAsync"/> throws
    /// <see cref="NotImplementedException"/> as it is in seeder scope.
    /// </summary>
    [Fact]
    public void GetUnembeddedForProfileAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateNpgsqlContext();
        var repo = new ProblemRepository(ctx);

        Action act = () => repo.GetUnembeddedForProfileAsync("gemini-embedding-001", 1536);
        act.Should().Throw<NotImplementedException>();
    }

    /// <summary>
    /// Verifies that <see cref="ProblemRepository.UpdateEmbeddingAsync"/> throws
    /// <see cref="NotImplementedException"/> as it is in seeder scope.
    /// </summary>
    [Fact]
    public void UpdateEmbeddingAsync_ThrowsNotImplementedException()
    {
        using var ctx = CreateNpgsqlContext();
        var repo = new ProblemRepository(ctx);

        Action act = () => repo.UpdateEmbeddingAsync(
            Guid.NewGuid(),
            new Vector(new float[1536]),
            "gemini-embedding-001",
            1536,
            DateTimeOffset.UtcNow);

        act.Should().Throw<NotImplementedException>();
    }

    /// <summary>
    /// Test-only <see cref="AppDbContext"/> subclass that adds a string value converter for
    /// <see cref="Problem.Embedding"/> so the InMemory provider can store and query it.
    /// </summary>
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Problem>()
                .Property(p => p.Embedding)
                .HasConversion(
                    v => v == null ? null : string.Join(",", v.ToArray()),
                    s => s == null ? null : new Vector(
                        s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(float.Parse)
                         .ToArray()));
        }
    }
}
