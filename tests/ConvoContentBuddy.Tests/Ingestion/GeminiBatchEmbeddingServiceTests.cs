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
/// Unit tests for <see cref="GeminiBatchEmbeddingService"/> covering synchronous
/// batchEmbedContents requests, chunking, result parsing, and error handling.
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
        IOptions<EmbeddingProfileOptions>? options = null) =>
        new(
            new HttpClient(handlerMock.Object),
            options ?? DefaultOptions(),
            NullLogger<GeminiBatchEmbeddingService>.Instance);

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
    /// Verifies that EmbedBatchAsync issues a POST to the batchEmbedContents endpoint.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_PostsToBatchEmbedContentsEndpoint()
    {
        HttpRequestMessage? capturedPost = null;
        var postCallCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedPost = req;
                postCallCount++;
                return JsonResponse(new { embeddings = new[] { new { values = new float[] { 0.1f, 0.2f, 0.3f, 0.4f } } } });
            });

        var item = Item("problem text");
        var service = CreateService(handler);

        await service.EmbedBatchAsync([item]);

        capturedPost.Should().NotBeNull();
        capturedPost!.Method.Should().Be(HttpMethod.Post);
        capturedPost.RequestUri!.AbsoluteUri.Should().Contain(":batchEmbedContents");
        capturedPost.RequestUri.AbsoluteUri.Should().NotContain(":batchEmbedContentsAsync");
        postCallCount.Should().Be(1);
    }

    // ── result parsing tests ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that embeddings are correctly mapped back to their corresponding problem IDs.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_MapsEmbeddingsToProblemIds()
    {
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
                JsonResponse(new
                {
                    embeddings = new[]
                    {
                        new { values = embedding1 },
                        new { values = embedding2 }
                    }
                }));

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
                JsonResponse(new { embeddings = embeddings.Select(e => new { values = e }).ToList() }));

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

    // ── chunking tests ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that inputs exceeding 100 items are split into separate POST requests of at most 100.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ChunksLargeInputInto100ItemBatches()
    {
        var items = Enumerable.Range(0, 250)
            .Select(i => (Id: Guid.NewGuid(), Text: $"text {i}"))
            .ToList();

        var postCallCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                postCallCount++;
                // Chunks are 100, 100, 50 — return exactly the right count for each.
                var count = postCallCount < 3 ? 100 : 50;
                var chunkEmbeddings = Enumerable.Range(0, count)
                    .Select(i => new { values = new float[] { i, i + 0.1f, i + 0.2f, i + 0.3f } })
                    .ToArray();
                return JsonResponse(new { embeddings = chunkEmbeddings });
            });

        var inputItems = items.Select(x => (x.Id, x.Text)).ToList();
        var service = CreateService(handler);

        var results = await service.EmbedBatchAsync(inputItems);

        postCallCount.Should().Be(3); // 100 + 100 + 50
        results.Should().HaveCount(250);
        for (var i = 0; i < 250; i++)
            results[i].ProblemId.Should().Be(items[i].Id);
    }

    // ── error handling tests ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a non-retryable 4xx response throws <see cref="IngestionException"/>
    /// and that only a single POST is issued (no retry).
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ThrowsIngestionExceptionOnJobFailure()
    {
        var postCallCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage _, CancellationToken _) =>
            {
                postCallCount++;
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            });

        var item = Item();
        var service = CreateService(handler);

        var act = async () => await service.EmbedBatchAsync([item]);

        await act.Should().ThrowAsync<IngestionException>();
        postCallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that the service retries on HTTP 429 and succeeds on a subsequent attempt.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_RetriesSubmissionOn429()
    {
        var postCallCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                postCallCount++;
                if (postCallCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);

                return JsonResponse(new { embeddings = new[] { new { values = new float[] { 1f, 2f, 3f, 4f } } } });
            });

        var item = Item();
        var service = CreateService(handler);

        var results = await service.EmbedBatchAsync([item]);

        postCallCount.Should().Be(2);
        results.Should().HaveCount(1);
        results[0].Embedding.Should().BeEquivalentTo(new float[] { 1f, 2f, 3f, 4f });
    }

    /// <summary>
    /// Verifies that a null response body throws <see cref="IngestionException"/>.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ThrowsIngestionExceptionOnNullResponseBody()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            });

        var item = Item();
        var service = CreateService(handler);

        var act = async () => await service.EmbedBatchAsync([item]);

        await act.Should().ThrowAsync<IngestionException>();
    }

    /// <summary>
    /// Verifies that a response with fewer embeddings than requested throws <see cref="IngestionException"/>.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ThrowsIngestionExceptionOnShortEmbeddingCount()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(new
            {
                embeddings = new[] { new { values = new float[] { 1f, 2f, 3f, 4f } } }
            }));

        var items = new List<(Guid ProblemId, string Text)>
        {
            (Guid.NewGuid(), "text one"),
            (Guid.NewGuid(), "text two")
        };
        var service = CreateService(handler);

        var act = async () => await service.EmbedBatchAsync(items);

        await act.Should().ThrowAsync<IngestionException>();
    }

    /// <summary>
    /// Verifies that embeddings with wrong dimensions throw <see cref="IngestionException"/>.
    /// </summary>
    [Fact]
    public async Task EmbedBatchAsync_ThrowsIngestionExceptionOnWrongDimensions()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(JsonResponse(new
            {
                // DefaultOptions sets Dimensions = 4, but we return 2-element vectors.
                embeddings = new[] { new { values = new float[] { 1f, 2f } } }
            }));

        var item = Item();
        var service = CreateService(handler);

        var act = async () => await service.EmbedBatchAsync([item]);

        await act.Should().ThrowAsync<IngestionException>();
    }
}

