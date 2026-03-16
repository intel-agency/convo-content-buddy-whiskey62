using System.Net;
using System.Text;
using System.Text.Json;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="GeminiEmbeddingService"/> covering embedding generation,
/// retry behaviour, and bounded concurrency.
/// </summary>
public class GeminiEmbeddingServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static IOptions<EmbeddingProfileOptions> DefaultOptions() =>
        Options.Create(new EmbeddingProfileOptions
        {
            ModelName = "gemini-embedding-001",
            Dimensions = 4,
            ApiKey = "test-key"
        });

    private static GeminiEmbeddingService CreateService(
        Mock<HttpMessageHandler> handlerMock,
        IOptions<EmbeddingProfileOptions>? options = null,
        int maxConcurrency = 4) =>
        new(
            new HttpClient(handlerMock.Object),
            options ?? DefaultOptions(),
            NullLogger<GeminiEmbeddingService>.Instance,
            maxConcurrency);

    private static HttpResponseMessage BuildEmbedResponse(float[] values)
    {
        var body = JsonSerializer.Serialize(new
        {
            embedding = new { values }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static Mock<HttpMessageHandler> SetupHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> factory)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(factory);
        return mock;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GenerateAsync returns an embedding with the correct number of dimensions.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_ReturnsEmbeddingWithCorrectDimensions()
    {
        var expectedValues = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var handler = SetupHandler((_, _) => BuildEmbedResponse(expectedValues));
        var service = CreateService(handler);

        var result = await service.GenerateAsync(["test text"]);

        result.Should().HaveCount(1);
        result[0].Vector.ToArray().Should().BeEquivalentTo(expectedValues);
    }

    /// <summary>
    /// Verifies that a 429 response is retried and the service succeeds on the second attempt.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_RetriesOn429AndSucceeds()
    {
        var expectedValues = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var callCount = 0;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                return BuildEmbedResponse(expectedValues);
            });

        var service = CreateService(handler);

        var result = await service.GenerateAsync(["test text"]);

        result.Should().HaveCount(1);
        result[0].Vector.ToArray().Should().BeEquivalentTo(expectedValues);
        callCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that an HttpRequestException is thrown after exhausting all retry attempts.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_ThrowsAfterMaxRetries()
    {
        var handler = SetupHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var service = CreateService(handler);

        var act = async () => await service.GenerateAsync(["test text"]);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Verifies that the semaphore limits concurrent Gemini API calls when GenerateAsync
    /// is called once with multiple inputs simultaneously.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_LimitsConcurrencyViaSemaphore()
    {
        const int maxConcurrency = 2;
        const int totalRequests = 6;
        var concurrentCallCount = 0;
        var maxObservedConcurrency = 0;
        var syncLock = new object();

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken ct) =>
            {
                lock (syncLock)
                {
                    concurrentCallCount++;
                    maxObservedConcurrency = Math.Max(maxObservedConcurrency, concurrentCallCount);
                }

                await Task.Delay(20, ct);

                lock (syncLock)
                {
                    concurrentCallCount--;
                }

                return BuildEmbedResponse([0.1f, 0.2f, 0.3f, 0.4f]);
            });

        var service = CreateService(handler, maxConcurrency: maxConcurrency);

        var texts = Enumerable.Range(0, totalRequests).Select(i => $"text {i}").ToList();

        // A single GenerateAsync call with multiple inputs now fans out in parallel internally.
        var results = await service.GenerateAsync(texts);

        results.Should().HaveCount(totalRequests);
        maxObservedConcurrency.Should().BeLessThanOrEqualTo(maxConcurrency);
        maxObservedConcurrency.Should().BeGreaterThan(1);
    }

    /// <summary>
    /// Verifies that Metadata returns expected model name and dimensions.
    /// </summary>
    [Fact]
    public void Metadata_ReturnsConfiguredValues()
    {
        var handler = new Mock<HttpMessageHandler>();
        var service = CreateService(handler);

        service.Metadata.DefaultModelId.Should().Be("gemini-embedding-001");
        service.Metadata.DefaultModelDimensions.Should().Be(4);
    }
}
