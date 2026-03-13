using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConvoContentBuddy.Data.Seeder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoContentBuddy.Data.Seeder.Services;

/// <summary>
/// HTTP transport for the LeetCode GraphQL API. Handles pagination, anti-bot headers,
/// retry logic with exponential back-off, and Cloudflare challenge detection.
/// </summary>
public sealed class LeetCodeGraphQlClient : ILeetCodeGraphQlClient
{
    private const string GraphQlEndpoint = "https://leetcode.com/graphql/";
    private const int DefaultPageSize = 100;
    private const int DefaultDelayMs = 750;

    private const string CatalogQuery =
        """
        query problemsetQuestionList($categorySlug: String, $limit: Int, $skip: Int, $filters: QuestionListFilterInput) {
          problemsetQuestionList: questionList(categorySlug: $categorySlug, limit: $limit, skip: $skip, filters: $filters) {
            total: totalNum
            questions: data {
              frontendQuestionId: questionFrontendId
              title
              titleSlug
              difficulty
              topicTags { name slug }
            }
          }
        }
        """;

    private const string QuestionDetailQuery =
        """
        query questionData($titleSlug: String!) {
          question(titleSlug: $titleSlug) {
            content
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LeetCodeGraphQlClient> _logger;
    private readonly LeetCodeClientOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="LeetCodeGraphQlClient"/>.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> used for all GraphQL requests.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="options">Optional client configuration; defaults are used when not provided.</param>
    public LeetCodeGraphQlClient(
        HttpClient httpClient,
        ILogger<LeetCodeGraphQlClient> logger,
        IOptions<LeetCodeClientOptions>? options = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options?.Value ?? new LeetCodeClientOptions();
    }

    /// <inheritdoc/>
    public async Task<LeetCodeRawCaptureDto> FetchAllProblemsAsync(CancellationToken cancellationToken = default)
    {
        var pageSize = _options.PageSize > 0 ? _options.PageSize : DefaultPageSize;
        var delayMs = _options.DelayBetweenRequestsMs > 0 ? _options.DelayBetweenRequestsMs : DefaultDelayMs;

        var capture = new LeetCodeRawCaptureDto();
        var allNodes = new List<LeetCodeQuestionNodeDto>();
        var skip = 0;
        int total;

        do
        {
            _logger.LogDebug("Fetching catalog page: skip={Skip}, limit={Limit}", skip, pageSize);

            var variables = new
            {
                categorySlug = "",
                limit = pageSize,
                skip,
                filters = new { }
            };

            var rawPage = await ExecuteRawGraphQlQueryAsync(
                CatalogQuery, variables, cancellationToken).ConfigureAwait(false);

            var response = JsonSerializer.Deserialize<LeetCodeCatalogResponseDto>(rawPage, JsonOptions);

            if (response?.Errors?.Count > 0)
            {
                var errorMessages = string.Join("; ", response.Errors.Select(e => e.Message));
                throw new HttpRequestException(
                    $"GraphQL catalog query returned errors at skip={skip}: {errorMessages}");
            }

            var questionList = response?.Data?.ProblemsetQuestionList;
            if (questionList is null)
            {
                throw new HttpRequestException(
                    $"GraphQL catalog response contained no question list data at skip={skip}");
            }

            if (questionList.Questions is null)
            {
                throw new HttpRequestException(
                    $"GraphQL catalog response contained no questions array at skip={skip}");
            }

            // Preserve the raw page JSON before any re-serialization can drop unmapped fields.
            capture.RawCatalogPages.Add(rawPage);

            total = questionList.Total;
            allNodes.AddRange(questionList.Questions);
            _logger.LogDebug("Fetched {Count}/{Total} catalog entries so far", allNodes.Count, total);

            skip += pageSize;

            if (skip < total)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        while (skip < total);

        // Enrich each catalog entry with per-problem content from the detail query.
        // Any failure here propagates and prevents a partial catalog from being persisted.
        for (var i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            var (content, rawDetail) = await FetchRawDetailAsync(node.TitleSlug, cancellationToken)
                .ConfigureAwait(false);

            node.Content = content;
            // Preserve the raw detail response so unmapped fields on the question object survive.
            capture.RawDetailResponses[node.TitleSlug] = rawDetail;

            _logger.LogDebug(
                "Fetched content for '{TitleSlug}' ({Current}/{Total})",
                node.TitleSlug, i + 1, allNodes.Count);

            if (i < allNodes.Count - 1)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        capture.MappedNodes = allNodes;
        capture.TotalCount = allNodes.Count;
        return capture;
    }

    /// <inheritdoc/>
    public async Task<string> FetchProblemContentAsync(string titleSlug, CancellationToken cancellationToken = default)
    {
        var (content, _) = await FetchRawDetailAsync(titleSlug, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <summary>
    /// Fetches the raw detail response for a single problem, returning both the HTML content
    /// string and the unmodified JSON response body so callers can capture the raw payload.
    /// </summary>
    private async Task<(string Content, string RawJson)> FetchRawDetailAsync(
        string titleSlug,
        CancellationToken cancellationToken)
    {
        var variables = new { titleSlug };
        var rawJson = await ExecuteRawGraphQlQueryAsync(
            QuestionDetailQuery, variables, cancellationToken).ConfigureAwait(false);

        var response = JsonSerializer.Deserialize<LeetCodeQuestionDetailResponseDto>(rawJson, JsonOptions);

        if (response?.Errors?.Count > 0)
        {
            var errorMessages = string.Join("; ", response.Errors.Select(e => e.Message));
            throw new HttpRequestException(
                $"GraphQL detail query returned errors for '{titleSlug}': {errorMessages}");
        }

        var content = response?.Data?.Question?.Content;
        if (content is null)
        {
            throw new HttpRequestException(
                $"GraphQL detail query returned null content for '{titleSlug}'");
        }

        return (content, rawJson);
    }

    private async Task<T?> ExecuteGraphQlQueryAsync<T>(
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        var raw = await ExecuteRawGraphQlQueryAsync(query, variables, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(raw, JsonOptions);
    }

    private async Task<string> ExecuteRawGraphQlQueryAsync(
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        var maxAttempts = _options.MaxRetryAttempts > 0 ? _options.MaxRetryAttempts : 3;

        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var requestBody = JsonSerializer.Serialize(new { query, variables });
                using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
                };

                AddAntisBotHeaders(request);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                // Cloudflare challenge detection — HTML response body indicates a challenge page
                if (IsHtmlResponse(response.Content.Headers.ContentType))
                {
                    _logger.LogWarning(
                        "Received HTML response (likely Cloudflare challenge) from LeetCode GraphQL endpoint");
                    throw new HttpRequestException(
                        "LeetCode returned an HTML response — Cloudflare challenge detected. Cannot retry.");
                }

                // Do not retry 403 — not a transient error
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Received 403 Forbidden from LeetCode GraphQL. Aborting.");
                    throw new HttpRequestException($"LeetCode GraphQL returned HTTP 403 Forbidden.");
                }

                // Retry-able status codes
                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    var delay = (int)Math.Pow(2, attempt - 1) * 1000;
                    _logger.LogWarning(
                        "Received {StatusCode} on attempt {Attempt}/{Max}. Retrying in {Delay}ms",
                        (int)response.StatusCode, attempt, maxAttempts, delay);

                    lastException = new HttpRequestException($"HTTP {(int)response.StatusCode}");
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw lastException;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt == maxAttempts)
            {
                throw;
            }
            catch (HttpRequestException ex) when (
                ex.Message.Contains("Cloudflare") || ex.Message.Contains("403"))
            {
                // Non-retriable — rethrow immediately
                throw;
            }
            catch (HttpRequestException ex)
            {
                var delay = (int)Math.Pow(2, attempt - 1) * 1000;
                _logger.LogWarning(ex,
                    "HttpRequestException on attempt {Attempt}/{Max}. Retrying in {Delay}ms",
                    attempt, maxAttempts, delay);
                lastException = ex;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw lastException ?? new HttpRequestException("GraphQL request failed after all retry attempts.");
    }

    private static void AddAntisBotHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Referer", "https://leetcode.com");
        request.Headers.TryAddWithoutValidation("Origin", "https://leetcode.com");
    }

    private static bool IsHtmlResponse(MediaTypeHeaderValue? contentType)
        => contentType?.MediaType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
}
