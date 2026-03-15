using ConvoContentBuddy.API.Brain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Implements <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> using the Gemini REST API.
/// </summary>
public sealed class GeminiEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingProfileOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="GeminiEmbeddingGenerator"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the Gemini API.</param>
    /// <param name="options">The active embedding profile options.</param>
    public GeminiEmbeddingGenerator(HttpClient httpClient, IOptions<EmbeddingProfileOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
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
        var embeddings = new List<Embedding<float>>();

        foreach (var text in values)
        {
            var requestBody = new GeminiEmbedRequest
            {
                Model = $"models/{_options.ModelName}",
                Content = new GeminiContent { Parts = [new GeminiPart { Text = text }] },
                OutputDimensionality = _options.Dimensions,
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.ModelName}:embedContent?key={_options.ApiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(
                cancellationToken: cancellationToken);

            var floatArray = result?.Embedding?.Values ?? [];
            embeddings.Add(new Embedding<float>(floatArray));
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
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
