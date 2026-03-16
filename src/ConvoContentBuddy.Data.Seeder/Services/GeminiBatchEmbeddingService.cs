using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Implements <see cref="IGeminiBatchEmbeddingService"/> using the Gemini async batch embedding
/// API. Submits a batch job, polls until completion, and maps results back to problem IDs.
/// </summary>
public sealed class GeminiBatchEmbeddingService : IGeminiBatchEmbeddingService
{
    private const int MaxRetryAttempts = 3;
    private const int MaxPollAttempts = 60;
    private const int DefaultPollIntervalMs = 5_000;

    private readonly HttpClient _httpClient;
    private readonly EmbeddingProfileOptions _options;
    private readonly ILogger<GeminiBatchEmbeddingService> _logger;
    private readonly int _pollIntervalMs;

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiBatchEmbeddingService"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the Gemini API.</param>
    /// <param name="options">The active embedding profile options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="pollIntervalMs">Milliseconds to wait between job status polls (default 5 000).</param>
    public GeminiBatchEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingProfileOptions> options,
        ILogger<GeminiBatchEmbeddingService> logger,
        int pollIntervalMs = DefaultPollIntervalMs)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _pollIntervalMs = pollIntervalMs;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(Guid ProblemId, float[] Embedding)>> EmbedBatchAsync(
        IReadOnlyList<(Guid ProblemId, string Text)> items,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Submitting async batch embedding job for {Count} items.", items.Count);

        var operationName = await SubmitBatchJobAsync(items, cancellationToken);

        _logger.LogInformation("Batch job submitted: {OperationName}. Polling for completion.", operationName);

        var completedJob = await PollUntilCompleteAsync(operationName, cancellationToken);

        var embeddings = completedJob.Response?.Embeddings ?? [];

        return items
            .Select((item, i) => (
                item.ProblemId,
                Embedding: i < embeddings.Count ? embeddings[i].Values : Array.Empty<float>()))
            .ToList();
    }

    private async Task<string> SubmitBatchJobAsync(
        IReadOnlyList<(Guid ProblemId, string Text)> items,
        CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.ModelName}:batchEmbedContentsAsync?key={_options.ApiKey}";

        var requestBody = new BatchJobRequest
        {
            Requests = items.Select(item => new BatchEmbedItem
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
                        "Batch job submission returned {StatusCode} on attempt {Attempt}/{Max}. Retrying in {Delay}ms.",
                        (int)response.StatusCode, attempt, MaxRetryAttempts, delay);

                    lastException = new HttpRequestException($"HTTP {(int)response.StatusCode}");

                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw lastException;
                }

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<BatchJobResponse>(
                    cancellationToken: cancellationToken);

                return result?.Name
                    ?? throw new IngestionException("Batch job submission returned a null operation name.");
            }
            catch (HttpRequestException ex) when (attempt < MaxRetryAttempts)
            {
                var delay = (int)Math.Pow(2, attempt - 1) * 1_000;
                _logger.LogWarning(ex,
                    "HttpRequestException on batch job submission attempt {Attempt}/{Max}. Retrying in {Delay}ms.",
                    attempt, MaxRetryAttempts, delay);
                lastException = ex;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }

        throw lastException ?? new HttpRequestException("Batch job submission failed after all retry attempts.");
    }

    private async Task<BatchJobStatusResponse> PollUntilCompleteAsync(
        string operationName,
        CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/{operationName}?key={_options.ApiKey}";

        for (var attempt = 1; attempt <= MaxPollAttempts; attempt++)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<BatchJobStatusResponse>(
                cancellationToken: cancellationToken);

            if (status?.Done == true)
            {
                if (status.Error != null)
                    throw new IngestionException(
                        $"Batch embedding job {operationName} failed: {status.Error.Message}");

                _logger.LogInformation("Batch job {OperationName} completed successfully.", operationName);
                return status;
            }

            _logger.LogDebug(
                "Batch job {OperationName} not yet complete (poll {Attempt}/{Max}). Waiting {Interval}ms.",
                operationName, attempt, MaxPollAttempts, _pollIntervalMs);

            if (attempt < MaxPollAttempts)
                await Task.Delay(_pollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        throw new IngestionException(
            $"Batch embedding job {operationName} did not complete after {MaxPollAttempts} poll attempts.");
    }

    // ── Request / response models ─────────────────────────────────────────────

    private sealed class BatchJobRequest
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

    private sealed class BatchJobResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    private sealed class BatchJobStatusResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("response")]
        public BatchJobResult? Response { get; set; }

        [JsonPropertyName("error")]
        public BatchJobError? Error { get; set; }
    }

    private sealed class BatchJobResult
    {
        [JsonPropertyName("embeddings")]
        public List<BatchEmbedding> Embeddings { get; set; } = [];
    }

    private sealed class BatchEmbedding
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = [];
    }

    private sealed class BatchJobError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
