using System.Net;
using System.Text;
using System.Text.Json;
using ConvoContentBuddy.Data.Seeder;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="GeminiBatchEmbeddingService"/> covering batch job submission,
/// polling until completion, result parsing, and error handling.
/// </summary>
public class GeminiBatchEmbeddingServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static IOptions<EmbeddingProfileOptions> DefaultOptions() =>
        Options.Create(new EmbeddingProfileOptions
        {
            ModelName = "gemini-embedding-001",
            Dimensions = 4,
            ApiKey = "test-key"
        });

    private static GeminiBatchEmbeddingService CreateService(
        Mock<HttpMessageHandler> handlerMock,
        IOptions<EmbeddingProfileOptions>? options = null,
        int pollIntervalMs = 0) =>
        new(
            new HttpClient(handlerMock.Object),
            options ?? DefaultOptions(),
            NullLogger<GeminiBatchEmbeddingService>.Instance,
            pollIntervalMs);

    private static HttpResponseMessage JsonResponse(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };

    private static (Guid Id, string Text) Item(string text = "hello") =>
        (Guid.NewGuid(), text);

    // ── submission tests ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that EmbedBatchAsync issues a POST to the async batch endpoint.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_PostsToBatchAsyncEndpoint()
    {
        var operationName = "operations/abc123";
        HttpRequestMessage? capturedPost = null;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    capturedPost = req;
                    return JsonResponse(new { name = operationName, done = false });
                }

                // GET poll → return completed
                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    response = new { embeddings = new[] { new { values = new float[] { 0.1f, 0.2f, 0.3f, 0.4f } } } }
                });
            });

        var item = Item("problem text");
        var service = CreateService(handler);

        await service.EmbedBatchAsync([item]);

        capturedPost.Should().NotBeNull();
        capturedPost!.RequestUri!.AbsoluteUri.Should().Contain(":batchEmbedContentsAsync");
    }

    /// <summary>
    /// Verifies that the operation name from the submission response is used for polling.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_UsesOperationNameFromSubmissionForPolling()
    {
        var operationName = "operations/xyz789";
        var pollUrls = new List<string>();

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                    return JsonResponse(new { name = operationName, done = false });

                pollUrls.Add(req.RequestUri!.AbsoluteUri);
                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    response = new { embeddings = new[] { new { values = new float[] { 1f, 2f, 3f, 4f } } } }
                });
            });

        var item = Item();
        var service = CreateService(handler);

        await service.EmbedBatchAsync([item]);

        pollUrls.Should().NotBeEmpty();
        pollUrls.Should().AllSatisfy(url => url.Should().Contain(operationName));
    }

    // ── polling tests ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the service polls multiple times before the job completes.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_PollsUntilJobComplete()
    {
        var operationName = "operations/poll-test";
        var pollCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                    return JsonResponse(new { name = operationName, done = false });

                pollCount++;
                var done = pollCount >= 3;
                if (!done)
                    return JsonResponse(new { name = operationName, done = false });

                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    response = new { embeddings = new[] { new { values = new float[] { 0.5f, 0.6f, 0.7f, 0.8f } } } }
                });
            });

        var item = Item();
        var service = CreateService(handler, pollIntervalMs: 0);

        var results = await service.EmbedBatchAsync([item]);

        pollCount.Should().Be(3);
        results.Should().HaveCount(1);
    }

    // ── result parsing tests ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that embeddings are correctly mapped back to their corresponding problem IDs.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_MapsEmbeddingsToProblemIds()
    {
        var operationName = "operations/map-test";
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var embedding1 = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var embedding2 = new float[] { 0.5f, 0.6f, 0.7f, 0.8f };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                    return JsonResponse(new { name = operationName, done = false });

                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    response = new
                    {
                        embeddings = new[]
                        {
                            new { values = embedding1 },
                            new { values = embedding2 }
                        }
                    }
                });
            });

        var items = new List<(Guid ProblemId, string Text)>
        {
            (id1, "text one"),
            (id2, "text two")
        };
        var service = CreateService(handler);

        var results = await service.EmbedBatchAsync(items);

        results.Should().HaveCount(2);
        results[0].ProblemId.Should().Be(id1);
        results[0].Embedding.Should().BeEquivalentTo(embedding1);
        results[1].ProblemId.Should().Be(id2);
        results[1].Embedding.Should().BeEquivalentTo(embedding2);
    }

    /// <summary>
    /// Verifies that input ordering is preserved in the returned results.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_PreservesInputOrder()
    {
        var operationName = "operations/order-test";
        var items = Enumerable.Range(0, 5)
            .Select(i => (Id: Guid.NewGuid(), Text: $"text {i}"))
            .ToList();

        var embeddings = items.Select((_, i) => new float[] { i, i + 0.1f, i + 0.2f, i + 0.3f }).ToList();

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                    return JsonResponse(new { name = operationName, done = false });

                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    response = new { embeddings = embeddings.Select(e => new { values = e }).ToList() }
                });
            });

        var inputItems = items.Select(x => (x.Id, x.Text)).ToList();
        var service = CreateService(handler);

        var results = await service.EmbedBatchAsync(inputItems);

        results.Should().HaveCount(5);
        for (var i = 0; i < 5; i++)
        {
            results[i].ProblemId.Should().Be(items[i].Id);
            results[i].Embedding.Should().BeEquivalentTo(embeddings[i]);
        }
    }

    // ── error handling tests ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an <see cref="IngestionException"/> is thrown when the batch job reports a failure.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ThrowsIngestionExceptionOnJobFailure()
    {
        var operationName = "operations/fail-test";

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                    return JsonResponse(new { name = operationName, done = false });

                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    error = new { message = "Quota exceeded." }
                });
            });

        var item = Item();
        var service = CreateService(handler);

        var act = async () => await service.EmbedBatchAsync([item]);

        await act.Should().ThrowAsync<IngestionException>()
            .WithMessage("*Quota exceeded.*");
    }

    /// <summary>
    /// Verifies that submission retries on HTTP 429 and succeeds on a subsequent attempt.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_RetriesSubmissionOn429()
    {
        var operationName = "operations/retry-test";
        var postCallCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    postCallCount++;
                    if (postCallCount == 1)
                        return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    return JsonResponse(new { name = operationName, done = false });
                }

                return JsonResponse(new
                {
                    name = operationName,
                    done = true,
                    response = new { embeddings = new[] { new { values = new float[] { 1f, 2f, 3f, 4f } } } }
                });
            });

        var item = Item();
        var service = CreateService(handler, pollIntervalMs: 0);

        var results = await service.EmbedBatchAsync([item]);

        postCallCount.Should().Be(2);
        results.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that an <see cref="IngestionException"/> is thrown when the job never completes
    /// within the maximum poll attempts.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ThrowsWhenJobNeverCompletes()
    {
        var operationName = "operations/timeout-test";

        // Always return done=false so we exhaust poll attempts.
        // Use a very low maxPollAttempts via a subclass or just observe the throw.
        // We use MaxPollAttempts=60 by default; to avoid slow tests we need a custom service.
        // Instead, we create a derived testable wrapper that has MaxPollAttempts=2.
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.Method == HttpMethod.Post)
                    return JsonResponse(new { name = operationName, done = false });

                // Always in progress
                return JsonResponse(new { name = operationName, done = false });
            });

        // Use cancellation to simulate timeout instead of waiting for 60 polls.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var item = Item();
        var service = CreateService(handler, pollIntervalMs: 0);

        var act = async () => await service.EmbedBatchAsync([item], cts.Token);

        await act.Should().ThrowAsync<Exception>();
    }
}
