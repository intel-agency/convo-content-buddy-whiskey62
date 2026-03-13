using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="LeetCodeGraphQlClient"/> covering deserialization,
/// pagination, retry behaviour, Cloudflare challenge detection, GraphQL error validation,
/// and per-problem content enrichment.
/// </summary>
public class LeetCodeGraphQlClientTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── helpers ────────────────────────────────────────────────────────────────

    private static LeetCodeGraphQlClient CreateClient(Mock<HttpMessageHandler> handlerMock)
    {
        var httpClient = new HttpClient(handlerMock.Object);
        return new LeetCodeGraphQlClient(
            httpClient,
            NullLogger<LeetCodeGraphQlClient>.Instance,
            null);
    }

    private static LeetCodeGraphQlClient CreateClientWithOptions(
        Mock<HttpMessageHandler> handlerMock,
        LeetCodeClientOptions options)
    {
        var httpClient = new HttpClient(handlerMock.Object);
        return new LeetCodeGraphQlClient(
            httpClient,
            NullLogger<LeetCodeGraphQlClient>.Instance,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    private static HttpResponseMessage JsonResponse(object body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return response;
    }

    private static HttpResponseMessage HtmlResponse(string html = "<html><body>Cloudflare</body></html>")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };
        return response;
    }

    private static object BuildCatalogResponse(int total, IEnumerable<object> questions)
        => new
        {
            data = new
            {
                problemsetQuestionList = new
                {
                    total,
                    questions
                }
            }
        };

    private static object BuildQuestion(string slug, string id, string title, string difficulty)
        => new
        {
            titleSlug = slug,
            frontendQuestionId = id,
            title,
            difficulty,
            topicTags = new[] { new { name = "Array", slug = "array" } }
        };

    private static object BuildDetailResponse(string content)
        => new
        {
            data = new
            {
                question = new { content }
            }
        };

    /// <summary>
    /// Sets up the handler to dispatch catalog and detail responses based on the request body.
    /// Catalog requests contain "problemsetQuestionList"; detail requests contain "questionData".
    /// </summary>
    private static void SetupDispatchedHandler(
        Mock<HttpMessageHandler> handler,
        Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(
                (req, _) => Task.FromResult(factory(req)));
    }

    private static bool IsCatalogRequest(HttpRequestMessage req)
    {
        var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        return body.Contains("problemsetQuestionList");
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a valid catalog response with two problems is correctly deserialized,
    /// and that Content is populated from the detail query for each problem.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_WithValidResponse_ReturnsDeserializedDtos()
    {
        var catalogBody = BuildCatalogResponse(2, new[]
        {
            BuildQuestion("two-sum", "1", "Two Sum", "Easy"),
            BuildQuestion("add-two-numbers", "2", "Add Two Numbers", "Medium")
        });

        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
        {
            if (IsCatalogRequest(req))
                return JsonResponse(catalogBody);
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var content = body.Contains("two-sum") ? "<p>Two Sum content</p>" : "<p>Add Two Numbers content</p>";
            return JsonResponse(BuildDetailResponse(content));
        });

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions { DelayBetweenRequestsMs = 0, PageSize = 100 });
        var result = await client.FetchAllProblemsAsync();

        result.Should().HaveCount(2);
        result[0].TitleSlug.Should().Be("two-sum");
        result[0].QuestionFrontendId.Should().Be("1");
        result[0].Title.Should().Be("Two Sum");
        result[0].Difficulty.Should().Be("Easy");
        result[0].TopicTags.Should().ContainSingle(t => t.Name == "Array");

        result[1].TitleSlug.Should().Be("add-two-numbers");
        result[1].QuestionFrontendId.Should().Be("2");
        result[1].Difficulty.Should().Be("Medium");
    }

    /// <summary>
    /// Verifies that the Content field is populated from the per-problem detail query on the live path.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_PopulatesContentFromDetailQuery()
    {
        const string expectedContent = "<p>Given an array of integers nums and an integer target...</p>";
        var catalogBody = BuildCatalogResponse(1, new[]
        {
            BuildQuestion("two-sum", "1", "Two Sum", "Easy")
        });

        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
            IsCatalogRequest(req)
                ? JsonResponse(catalogBody)
                : JsonResponse(BuildDetailResponse(expectedContent)));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions { DelayBetweenRequestsMs = 0, PageSize = 100 });
        var result = await client.FetchAllProblemsAsync();

        result.Should().HaveCount(1);
        result[0].Content.Should().Be(expectedContent);
    }

    /// <summary>
    /// Verifies that the client issues two HTTP catalog requests when total exceeds the page size,
    /// plus one detail request per problem, and returns all problems combined.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_PaginatesCorrectly_WhenTotalExceedsPageSize()
    {
        var page1Questions = Enumerable.Range(1, 2)
            .Select(i => BuildQuestion($"slug-{i}", i.ToString(), $"Problem {i}", "Easy"))
            .ToArray();

        var page2Questions = Enumerable.Range(3, 1)
            .Select(i => BuildQuestion($"slug-{i}", i.ToString(), $"Problem {i}", "Easy"))
            .ToArray();

        var page1 = BuildCatalogResponse(3, page1Questions);
        var page2 = BuildCatalogResponse(3, page2Questions);

        var catalogCallCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
        {
            if (IsCatalogRequest(req))
                return catalogCallCount++ == 0 ? JsonResponse(page1) : JsonResponse(page2);
            return JsonResponse(BuildDetailResponse("<p>content</p>"));
        });

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 2,
            MaxRetryAttempts = 1
        });

        var result = await client.FetchAllProblemsAsync();

        result.Should().HaveCount(3);
        // 2 catalog pages + 3 detail requests = 5 total
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(5),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Verifies that a valid question detail response returns the HTML content string.
    /// </summary>
    [Fact]
    public async Task FetchProblemContentAsync_WithValidResponse_ReturnsHtmlContent()
    {
        const string expectedHtml = "<p>Given an array of integers...</p>";
        var responseBody = new
        {
            data = new
            {
                question = new { content = expectedHtml }
            }
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(responseBody));

        var client = CreateClient(handler);
        var result = await client.FetchProblemContentAsync("two-sum");

        result.Should().Be(expectedHtml);
    }

    /// <summary>
    /// Verifies that the client retries once after a 429 response and succeeds on the second attempt.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_Retries_On429Response()
    {
        var successResponse = BuildCatalogResponse(1,
            new[] { BuildQuestion("two-sum", "1", "Two Sum", "Easy") });

        var catalogCallCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
        {
            if (IsCatalogRequest(req))
            {
                return catalogCallCount++ == 0
                    ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    : JsonResponse(successResponse);
            }
            return JsonResponse(BuildDetailResponse("<p>content</p>"));
        });

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 3
        });

        var result = await client.FetchAllProblemsAsync();

        result.Should().HaveCount(1);
        // 2 catalog requests (1 retry) + 1 detail request = 3 total
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(3),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Verifies that the client retries once after a 503 response and succeeds on the second attempt.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_Retries_On503Response()
    {
        var successResponse = BuildCatalogResponse(1,
            new[] { BuildQuestion("two-sum", "1", "Two Sum", "Easy") });

        var catalogCallCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
        {
            if (IsCatalogRequest(req))
            {
                return catalogCallCount++ == 0
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : JsonResponse(successResponse);
            }
            return JsonResponse(BuildDetailResponse("<p>content</p>"));
        });

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 3
        });

        var result = await client.FetchAllProblemsAsync();

        result.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that a Cloudflare challenge (HTML content-type) causes an immediate
    /// <see cref="HttpRequestException"/> without further retries.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsOnCloudflareChallenge()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(HtmlResponse());

        var client = CreateClient(handler);

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();

        // Should NOT retry — only one request made
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Verifies that all retry attempts returning 429 ultimately cause an exception.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsAfterMaxRetries()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 3
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();

        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(3),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Verifies that a 200 response containing a top-level GraphQL <c>errors</c> array
    /// causes <see cref="HttpRequestException"/> rather than silently returning an empty catalog.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsOnGraphQlErrors()
    {
        var errorResponse = new
        {
            data = (object?)null,
            errors = new[] { new { message = "Authentication required" } }
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(errorResponse));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*GraphQL catalog query returned errors*");
    }

    /// <summary>
    /// Verifies that a 200 response with null <c>data</c> (no <c>problemsetQuestionList</c>)
    /// causes <see cref="HttpRequestException"/> rather than silently returning an empty catalog.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsOnNullData()
    {
        var nullDataResponse = new { data = (object?)null };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(nullDataResponse));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*no question list data*");
    }

    /// <summary>
    /// Verifies that a GraphQL error on a mid-pagination page causes <see cref="HttpRequestException"/>
    /// instead of persisting the partial result as a snapshot.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsOnMidPaginationMalformedPage()
    {
        var page1Questions = Enumerable.Range(1, 2)
            .Select(i => BuildQuestion($"slug-{i}", i.ToString(), $"Problem {i}", "Easy"))
            .ToArray();
        var page1 = BuildCatalogResponse(3, page1Questions);

        var malformedPage2 = new
        {
            data = (object?)null,
            errors = new[] { new { message = "Rate limit exceeded" } }
        };

        var catalogCallCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
        {
            if (IsCatalogRequest(req))
                return catalogCallCount++ == 0 ? JsonResponse(page1) : JsonResponse(malformedPage2);
            return JsonResponse(BuildDetailResponse("<p>content</p>"));
        });

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 2,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*GraphQL catalog query returned errors*");
    }

    /// <summary>
    /// Verifies that <see cref="HttpRequestException"/> is thrown when a per-problem detail query
    /// returns GraphQL errors, preventing partial catalog data from being returned.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsWhenDetailQueryReturnsGraphQlErrors()
    {
        var catalogBody = BuildCatalogResponse(1, new[]
        {
            BuildQuestion("two-sum", "1", "Two Sum", "Easy")
        });

        var detailErrorResponse = new
        {
            data = (object?)null,
            errors = new[] { new { message = "Problem not available" } }
        };

        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
            IsCatalogRequest(req)
                ? JsonResponse(catalogBody)
                : JsonResponse(detailErrorResponse));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*GraphQL detail query returned errors*");
    }

    /// <summary>
    /// Verifies that <see cref="HttpRequestException"/> is thrown when a per-problem detail query
    /// returns a null content field, preventing incomplete catalog data from being returned.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsWhenDetailQueryReturnsNullContent()
    {
        var catalogBody = BuildCatalogResponse(1, new[]
        {
            BuildQuestion("two-sum", "1", "Two Sum", "Easy")
        });

        var nullContentResponse = new
        {
            data = new
            {
                question = new { content = (string?)null }
            }
        };

        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
            IsCatalogRequest(req)
                ? JsonResponse(catalogBody)
                : JsonResponse(nullContentResponse));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*null content*");
    }

    /// <summary>
    /// Verifies that <see cref="HttpRequestException"/> is thrown when a per-problem detail query
    /// exhausts all retry attempts (e.g. 429 on every attempt), preventing partial catalog data.
    /// </summary>
    [Fact]
    public async Task FetchAllProblemsAsync_ThrowsWhenDetailQueryExhaustsRetries()
    {
        var catalogBody = BuildCatalogResponse(1, new[]
        {
            BuildQuestion("two-sum", "1", "Two Sum", "Easy")
        });

        var handler = new Mock<HttpMessageHandler>();
        SetupDispatchedHandler(handler, req =>
            IsCatalogRequest(req)
                ? JsonResponse(catalogBody)
                : new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            PageSize = 100,
            MaxRetryAttempts = 2
        });

        var act = async () => await client.FetchAllProblemsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that <see cref="LeetCodeGraphQlClient.FetchProblemContentAsync"/> throws <see cref="HttpRequestException"/>
    /// when the detail response contains GraphQL errors, instead of silently returning null.
    /// </summary>
    [Fact]
    public async Task FetchProblemContentAsync_ThrowsOnGraphQlErrors()
    {
        var errorResponse = new
        {
            data = (object?)null,
            errors = new[] { new { message = "Problem not found" } }
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(errorResponse));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchProblemContentAsync("two-sum");

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*GraphQL detail query returned errors*");
    }

    /// <summary>
    /// Verifies that <see cref="LeetCodeGraphQlClient.FetchProblemContentAsync"/> throws <see cref="HttpRequestException"/>
    /// when the detail response content field is null, instead of silently returning null.
    /// </summary>
    [Fact]
    public async Task FetchProblemContentAsync_ThrowsWhenContentIsNull()
    {
        var nullContentResponse = new
        {
            data = new
            {
                question = new { content = (string?)null }
            }
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(nullContentResponse));

        var client = CreateClientWithOptions(handler, new LeetCodeClientOptions
        {
            DelayBetweenRequestsMs = 0,
            MaxRetryAttempts = 1
        });

        var act = async () => await client.FetchProblemContentAsync("two-sum");

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*null content*");
    }
}
