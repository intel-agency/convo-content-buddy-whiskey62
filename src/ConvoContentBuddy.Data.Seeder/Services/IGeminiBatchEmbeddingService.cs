namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Provides batch embedding generation for problem texts using the Gemini API.
/// </summary>
public interface IGeminiBatchEmbeddingService
{
    /// <summary>
    /// Generates embeddings for a batch of problem texts, returning results keyed by problem ID.
    /// </summary>
    /// <param name="items">
    /// Pairs of problem ID and the text to embed. The ordering of the returned list
    /// matches the input ordering.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A read-only list of tuples pairing each <see cref="Guid"/> problem ID with its
    /// corresponding embedding float array.
    /// </returns>
    Task<IReadOnlyList<(Guid ProblemId, float[] Embedding)>> EmbedBatchAsync(
        IReadOnlyList<(Guid ProblemId, string Text)> items,
        CancellationToken cancellationToken = default);
}
