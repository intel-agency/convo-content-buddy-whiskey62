namespace ConvoContentBuddy.Data.Entities;

/// <summary>
/// Represents a coding problem with optional vector embedding for semantic search.
/// </summary>
public class Problem
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the URL-friendly slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the external question identifier.</summary>
    public int QuestionId { get; set; }

    /// <summary>Gets or sets the problem title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the difficulty level (e.g., Easy, Medium, Hard).</summary>
    public string Difficulty { get; set; } = string.Empty;

    /// <summary>Gets or sets the full problem description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the vector embedding for semantic similarity search.</summary>
    public Pgvector.Vector? Embedding { get; set; }

    /// <summary>Gets or sets the model name used to generate the embedding.</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>Gets or sets the number of dimensions in the embedding vector.</summary>
    public int? EmbeddingDimensions { get; set; }

    /// <summary>Gets or sets when the embedding was generated.</summary>
    public DateTimeOffset? EmbeddingGeneratedAt { get; set; }

    /// <summary>Gets or sets when this problem was first seeded.</summary>
    public DateTimeOffset SeededAt { get; set; }

    /// <summary>Gets or sets when this problem was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the associated problem-tag join records.</summary>
    public ICollection<ProblemTag> ProblemTags { get; set; } = [];
}
