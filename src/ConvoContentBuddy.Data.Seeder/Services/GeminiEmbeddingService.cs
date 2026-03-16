using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// Implements <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> using the Gemini REST API.
/// Adds a <see cref="SemaphoreSlim"/> to limit concurrent requests and manual retry with
/// exponential back-off for HTTP 429 and 5xx responses.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int MaxRetryAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly EmbeddingProfileOptions _options;
    private readonly ILogger<GeminiEmbeddingService> _logger;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiEmbeddingService"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the Gemini API.</param>
    /// <param name="options">The active embedding profile options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent Gemini API calls (default 4).</param>
    public GeminiEmbeddingService(
        HttpClient httpClient,
        IOptions<EmbeddingProfileOptions> options,
        ILogger<GeminiEmbeddingService> logger,
        int maxConcurrency = 4)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <inheritdoc/>
    public EmbeddingGeneratorMetadata Metadata =>
        new("Gemini", null, _options.ModelName, _options.Dimensions);

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = values.Select(async text =>
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var floatArray = await EmbedWithRetryAsync(text, cancellationToken);
                return new Embedding<float>(floatArray);
            }
            finally
            {
                _semaphore.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);
        return new GeneratedEmbeddings<Embedding<float>>(results.ToList());
    }

    private async Task<float[]> EmbedWithRetryAsync(string text, CancellationToken cancellationToken)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.ModelName}:embedContent?key={_options.ApiKey}";

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            var requestBody = new GeminiEmbedRequest
            {
                Model = $"models/{_options.ModelName}",
                Content = new GeminiContent { Parts = [new GeminiPart { Text = text }] },
                OutputDimensionality = _options.Dimensions,
            };

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
                {
                    var delay = (int)Math.Pow(2, attempt - 1) * 1000;
                    _logger.LogWarning(
                        "Gemini embedding API returned {StatusCode} on attempt {Attempt}/{Max}. Retrying in {Delay}ms.",
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

                var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(
                    cancellationToken: cancellationToken);

                return result?.Embedding?.Values ?? [];
            }
            catch (HttpRequestException ex) when (attempt < MaxRetryAttempts)
            {
                var delay = (int)Math.Pow(2, attempt - 1) * 1000;
                _logger.LogWarning(ex,
                    "HttpRequestException on embedding attempt {Attempt}/{Max}. Retrying in {Delay}ms.",
                    attempt, MaxRetryAttempts, delay);
                lastException = ex;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }

        throw lastException ?? new HttpRequestException("Gemini embedding request failed after all retry attempts.");
    }

    /// <inheritdoc/>
    public TService? GetService<TService>(object? key = null) where TService : class =>
        this as TService;

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc/>
    public void Dispose() { }

    private sealed class GeminiEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; } = new();

        [JsonPropertyName("outputDimensionality")]
        public int OutputDimensionality { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbedding? Embedding { get; set; }
    }

    private sealed class GeminiEmbedding
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = [];
    }
}
