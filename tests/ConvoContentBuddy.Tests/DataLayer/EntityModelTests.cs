using ConvoContentBuddy.Data.Entities;
using Pgvector;

namespace ConvoContentBuddy.Tests.DataLayer;

/// <summary>
/// Tests that entity models can be constructed and have properties assigned correctly.
/// </summary>
public class EntityModelTests
{
    /// <summary>Verifies <see cref="Problem"/> can be constructed with all required and optional properties.</summary>
    [Fact]
    public void Problem_CanBeConstructedWithRequiredProperties()
    {
        var embedding = new Vector(new float[1536]);
        var now = DateTimeOffset.UtcNow;

        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Slug = "two-sum",
            QuestionId = 1,
            Title = "Two Sum",
            Difficulty = "Easy",
            Description = "Given an array of integers...",
            Embedding = embedding,
            EmbeddingModel = "text-embedding-3-small",
            EmbeddingDimensions = 1536,
            EmbeddingGeneratedAt = now,
            SeededAt = now,
            UpdatedAt = now
        };

        problem.Slug.Should().Be("two-sum");
        problem.QuestionId.Should().Be(1);
        problem.Title.Should().Be("Two Sum");
        problem.Difficulty.Should().Be("Easy");
        problem.Description.Should().NotBeNullOrEmpty();
        problem.Embedding.Should().NotBeNull();
        problem.EmbeddingModel.Should().Be("text-embedding-3-small");
        problem.EmbeddingDimensions.Should().Be(1536);
        problem.EmbeddingGeneratedAt.Should().Be(now);
        problem.SeededAt.Should().Be(now);
        problem.UpdatedAt.Should().Be(now);
        problem.ProblemTags.Should().BeEmpty();
    }

    /// <summary>Verifies nullable embedding provenance fields on <see cref="Problem"/> default to null.</summary>
    [Fact]
    public void Problem_NullableEmbeddingProvenanceFieldsDefaultToNull()
    {
        var problem = new Problem
        {
            Slug = "reverse-linked-list",
            QuestionId = 206,
            Title = "Reverse Linked List",
            Difficulty = "Easy",
            Description = "Reverse a singly linked list.",
            SeededAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        problem.Embedding.Should().BeNull();
        problem.EmbeddingModel.Should().BeNull();
        problem.EmbeddingDimensions.Should().BeNull();
        problem.EmbeddingGeneratedAt.Should().BeNull();
    }

    /// <summary>Verifies <see cref="Tag"/> can be constructed with a <c>Name</c> property.</summary>
    [Fact]
    public void Tag_CanBeConstructedWithNameProperty()
    {
        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "dynamic-programming"
        };

        tag.Name.Should().Be("dynamic-programming");
        tag.ProblemTags.Should().BeEmpty();
    }

    /// <summary>Verifies <see cref="Problem"/> accepts each of the three allowed difficulty values at the entity level.</summary>
    [Theory]
    [InlineData("Easy")]
    [InlineData("Medium")]
    [InlineData("Hard")]
    public void Problem_Difficulty_AcceptsAllValidValues(string difficulty)
    {
        var problem = new Problem
        {
            Slug = "test-problem",
            QuestionId = 999,
            Title = "Test Problem",
            Difficulty = difficulty,
            Description = "A test problem.",
            SeededAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        problem.Difficulty.Should().Be(difficulty);
    }
}

