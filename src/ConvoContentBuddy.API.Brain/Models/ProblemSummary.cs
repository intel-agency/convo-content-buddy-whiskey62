using ConvoContentBuddy.Data.Entities;

namespace ConvoContentBuddy.API.Brain.Models;

/// <summary>
/// DTO representing a problem search result with a computed cosine similarity score.
/// </summary>
public record ProblemSummary
{
    /// <summary>Gets the URL-friendly slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the problem title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the difficulty level (Easy, Medium, or Hard).</summary>
    public required string Difficulty { get; init; }

    /// <summary>Gets the tag names associated with the problem.</summary>
    public required List<string> Tags { get; init; }

    /// <summary>Gets the cosine similarity score relative to the query vector.</summary>
    public required double SimilarityScore { get; init; }

    /// <summary>
    /// Creates a <see cref="ProblemSummary"/> from a <see cref="Problem"/> entity,
    /// computing cosine similarity against the provided query vector.
    /// </summary>
    /// <param name="problem">The problem entity (must have <see cref="Problem.ProblemTags"/> loaded).</param>
    /// <param name="queryVector">The query embedding to compare against.</param>
    /// <returns>A populated <see cref="ProblemSummary"/>.</returns>
    public static ProblemSummary FromProblem(Problem problem, float[] queryVector)
    {
        var embedding = problem.Embedding?.ToArray() ?? [];
        var similarity = ComputeCosineSimilarity(queryVector, embedding);

        return new ProblemSummary
        {
            Slug = problem.Slug,
            Title = problem.Title,
            Difficulty = problem.Difficulty,
            Tags = problem.ProblemTags.Select(pt => pt.Tag.Name).ToList(),
            SimilarityScore = similarity,
        };
    }

    private static double ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0.0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0.0 ? 0.0 : dot / denom;
    }
}
