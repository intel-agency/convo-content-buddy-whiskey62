using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Implements <see cref="IGeminiBatchEmbeddingService"/> using the synchronous Gemini
/// <c>batchEmbedContents</c> API, chunking inputs into groups of 100.
/// </summary>
public sealed class GeminiBatchEmbeddingService : IGeminiBatchEmbeddingService
{
    private const int MaxRetryAttempts = 3;
    private const int ChunkSize = 100;

    private readonly HttpClient _httpClient;
    private readonly EmbeddingProfileOptions _options;
    private readonly ILogger<GeminiBatchEmbeddingService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiBatchEmbeddingService"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the Gemini API.</param>
    /// <param name="options">The active embedding profile options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GeminiBatchEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingProfileOptions> options,
        ILogger<GeminiBatchEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(Guid ProblemId, float[] Embedding)>> EmbedBatchAsync(
        IReadOnlyList<(Guid ProblemId, string Text)> items,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Embedding {Count} items via batchEmbedContents.", items.Count);

        var results = new List<(Guid ProblemId, float[] Embedding)>(items.Count);

        for (var offset = 0; offset < items.Count; offset += ChunkSize)
        {
            var chunk = items.Skip(offset).Take(ChunkSize).ToList();
            var chunkEmbeddings = await SendChunkAsync(chunk, cancellationToken);

            for (var i = 0; i < chunk.Count; i++)
            {
                results.Add((chunk[i].ProblemId, chunkEmbeddings[i].Values));
            }
        }

        return results;
    }

    private async Task<List<BatchEmbedding>> SendChunkAsync(
        IReadOnlyList<(Guid ProblemId, string Text)> chunk,
        CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.ModelName}:batchEmbedContents?key={_options.ApiKey}";

        var requestBody = new BatchEmbedRequest
        {
            Requests = chunk.Select(item => new BatchEmbedItem
            {
                Model = $"models/{_options.ModelName}",
                Content = new EmbedContent { Parts = [new EmbedPart { Text = item.Text }] },
                OutputDimensionality = _options.Dimensions,
            }).ToList()
        };

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
                {
                    var delay = (int)Math.Pow(2, attempt - 1) * 1_000;
                    _logger.LogWarning(
                        "batchEmbedContents returned {StatusCode} on attempt {Attempt}/{Max}. Retrying in {Delay}ms.",
                        (int)response.StatusCode, attempt, MaxRetryAttempts, delay);

                    lastException = new HttpRequestException($"HTTP {(int)response.StatusCode}");

                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw lastException;
                }

                // Non-retryable 4xx: surface immediately without consuming retry budget.
                if (!response.IsSuccessStatusCode)
                {
                    throw new IngestionException(
                        $"batchEmbedContents returned non-retryable status {(int)response.StatusCode}.");
                }

                var result = await response.Content.ReadFromJsonAsync<BatchEmbedResponse>(
                    cancellationToken: cancellationToken);

                if (result is null)
                {
                    throw new IngestionException(
                        "batchEmbedContents returned a null or unparseable response body.");
                }

                if (result.Embeddings.Count != chunk.Count)
                {
                    throw new IngestionException(
                        $"batchEmbedContents returned {result.Embeddings.Count} embeddings for {chunk.Count} requested items.");
                }

                for (var i = 0; i < result.Embeddings.Count; i++)
                {
                    if (result.Embeddings[i].Values.Length != _options.Dimensions)
                    {
                        throw new IngestionException(
                            $"Embedding {i} has {result.Embeddings[i].Values.Length} dimensions but {_options.Dimensions} were expected.");
                    }
                }

                return result.Embeddings;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetryAttempts)
            {
                var delay = (int)Math.Pow(2, attempt - 1) * 1_000;
                _logger.LogWarning(ex,
                    "HttpRequestException on batchEmbedContents attempt {Attempt}/{Max}. Retrying in {Delay}ms.",
                    attempt, MaxRetryAttempts, delay);
                lastException = ex;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }

        throw lastException ?? new HttpRequestException("batchEmbedContents failed after all retry attempts.");
    }

    // ── Request / response models ─────────────────────────────────────────────

    private sealed class BatchEmbedRequest
    {
        [JsonPropertyName("requests")]
        public List<BatchEmbedItem> Requests { get; set; } = [];
    }

    private sealed class BatchEmbedItem
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public EmbedContent Content { get; set; } = new();

        [JsonPropertyName("outputDimensionality")]
        public int OutputDimensionality { get; set; }
    }

    private sealed class EmbedContent
    {
        [JsonPropertyName("parts")]
        public List<EmbedPart> Parts { get; set; } = [];
    }

    private sealed class EmbedPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class BatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<BatchEmbedding> Embeddings { get; set; } = [];
    }

    private sealed class BatchEmbedding
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = [];
    }
}
