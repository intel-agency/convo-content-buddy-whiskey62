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
/// Tests that require raw SQL (e.g., vector search) are verified at the contract level only,
/// since the InMemory provider does not support SQL execution.
/// </summary>
public class ProblemRepositoryTests
{
    private static AppDbContext CreateNpgsqlContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost", o => o.UseVector())
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
    /// Verifies that <see cref="ProblemRepository.CountAsync"/> exists and returns
    /// <see cref="Task{T}"/> of <see cref="int"/>.
    /// </summary>
    [Fact]
    public void CountAsync_HasCorrectReturnType()
    {
        var method = typeof(ProblemRepository).GetMethod("CountAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<int>));
    }

    /// <summary>
    /// Verifies that <see cref="ProblemRepository.SearchByVectorAsync"/> exists and
    /// accepts a <see cref="Vector"/>, model name, dimensions, and limit.
    /// </summary>
    [Fact]
    public void SearchByVectorAsync_HasCorrectSignature()
    {
        var method = typeof(ProblemRepository).GetMethod("SearchByVectorAsync");
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(Vector));
        parameters.Should().Contain(p => p.ParameterType == typeof(string));
        parameters.Where(p => p.ParameterType == typeof(int)).Should().HaveCountGreaterThanOrEqualTo(2,
            because: "SearchByVectorAsync must accept both dimensions and limit as int parameters");
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
}
