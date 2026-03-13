using System.Text.Json;
using ConvoContentBuddy.Data.Seeder;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="ResilientLeetCodeDataSource"/> covering the live-first,
/// snapshot-fallback strategy including raw-capture persistence and replay mapping.
/// </summary>
public class ResilientLeetCodeDataSourceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static LeetCodeRawCaptureDto BuildRawCapture(int count = 2, bool withContent = true)
    {
        var nodes = Enumerable.Range(1, count)
            .Select(i => new LeetCodeQuestionNodeDto
            {
                TitleSlug = $"slug-{i}",
                QuestionFrontendId = i.ToString(),
                Title = $"Problem {i}",
                Difficulty = "Easy",
                TopicTags = [new LeetCodeTopicTagDto { Name = "Array", Slug = "array" }],
                Content = withContent ? $"<p>Content for problem {i}</p>" : null
            })
            .ToList();

        // Build raw catalog page JSON the way the live client would return it
        var catalogPageJson = JsonSerializer.Serialize(new
        {
            data = new
            {
                problemsetQuestionList = new
                {
                    total = count,
                    questions = nodes.Select(n => new
                    {
                        titleSlug = n.TitleSlug,
                        frontendQuestionId = n.QuestionFrontendId,
                        title = n.Title,
                        difficulty = n.Difficulty,
                        topicTags = n.TopicTags.Select(t => new { name = t.Name, slug = t.Slug })
                    })
                }
            }
        });

        var rawDetailResponses = nodes.ToDictionary(
            n => n.TitleSlug,
            n => JsonSerializer.Serialize(new
            {
                data = new
                {
                    question = new { content = n.Content }
                }
            }));

        return new LeetCodeRawCaptureDto
        {
            RawCatalogPages = [catalogPageJson],
            RawDetailResponses = rawDetailResponses,
            TotalCount = count,
            MappedNodes = nodes
        };
    }

    /// <summary>
    /// Builds a replay capture (no MappedNodes, as happens when loading from a snapshot).
    /// </summary>
    private static LeetCodeRawCaptureDto BuildReplayCapture(int count = 2, bool withContent = true)
    {
        var capture = BuildRawCapture(count, withContent);
        return new LeetCodeRawCaptureDto
        {
            RawCatalogPages = capture.RawCatalogPages,
            RawDetailResponses = capture.RawDetailResponses,
            TotalCount = capture.TotalCount
            // MappedNodes intentionally omitted — simulates [JsonIgnore] on deserialization
        };
    }

    private static ResilientLeetCodeDataSource CreateSource(
        Mock<ILeetCodeGraphQlClient> clientMock,
        Mock<ISnapshotService> snapshotMock,
        ILogger<ResilientLeetCodeDataSource>? logger = null)
        => new(
            clientMock.Object,
            snapshotMock.Object,
            logger ?? NullLogger<ResilientLeetCodeDataSource>.Instance);

    /// <summary>
    /// When the live client succeeds, the raw capture is persisted and the
    /// mapped problems (with Content) are returned.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveSucceeds_PersistsSnapshotAndReturnsDtos()
    {
        var capture = BuildRawCapture(5, withContent: true);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(capture);

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.PersistSnapshotAsync(It.IsAny<LeetCodeRawCaptureDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(5);
        result[0].TitleSlug.Should().Be("slug-1");

        snapshotMock.Verify(
            s => s.PersistSnapshotAsync(
                It.Is<LeetCodeRawCaptureDto>(c => c.MappedNodes.Count == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// When the live client succeeds, the raw capture passed to the snapshot layer preserves
    /// both <c>RawCatalogPages</c> and <c>RawDetailResponses</c>.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveSucceeds_PersistsRawCaptureWithPages()
    {
        var capture = BuildRawCapture(3, withContent: true);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(capture);

        LeetCodeRawCaptureDto? capturedArg = null;
        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.PersistSnapshotAsync(It.IsAny<LeetCodeRawCaptureDto>(), It.IsAny<CancellationToken>()))
            .Callback<LeetCodeRawCaptureDto, CancellationToken>((c, _) => capturedArg = c)
            .Returns(Task.CompletedTask);

        var source = CreateSource(clientMock, snapshotMock);
        await source.FetchCatalogAsync();

        capturedArg.Should().NotBeNull();
        capturedArg!.RawCatalogPages.Should().HaveCount(1);
        capturedArg.RawDetailResponses.Should().HaveCount(3);
        capturedArg.TotalCount.Should().Be(3);
        capturedArg.MappedNodes.Should().HaveCount(3);
        capturedArg.MappedNodes[0].TitleSlug.Should().Be("slug-1");
    }

    /// <summary>
    /// When the live client throws, the snapshot fallback returns the cached problems
    /// mapped from the preserved raw JSON strings.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveFails_FallsBackToSnapshot()
    {
        var replayCapture = BuildReplayCapture(3, withContent: true);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayCapture);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(3);
        result[0].TitleSlug.Should().Be("slug-1");
    }

    /// <summary>
    /// When the live client fails due to a detail query failure, falls back to snapshot
    /// and does NOT persist a partial snapshot.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_DetailFetchFails_FallsBackToSnapshotWithoutPersisting()
    {
        var replayCapture = BuildReplayCapture(2, withContent: true);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("GraphQL detail query returned errors for 'slug-1': Problem not available"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayCapture);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(2);

        // Snapshot must NOT be persisted with incomplete data
        snapshotMock.Verify(
            s => s.PersistSnapshotAsync(It.IsAny<LeetCodeRawCaptureDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// When both the live client and snapshot fail, an <see cref="IngestionException"/> is thrown.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveFailsAndNoSnapshot_ThrowsIngestionException()
    {
        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeetCodeRawCaptureDto?)null);

        var source = CreateSource(clientMock, snapshotMock);

        var act = async () => await source.FetchCatalogAsync();

        await act.Should().ThrowAsync<IngestionException>()
            .WithMessage("*No LeetCode data available*");
    }

    /// <summary>
    /// Verifies that a warning-level log entry is written when falling back to the snapshot.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveFails_LogsWarning()
    {
        var replayCapture = BuildReplayCapture(1, withContent: false);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayCapture);

        var loggerMock = new Mock<ILogger<ResilientLeetCodeDataSource>>();

        var source = CreateSource(clientMock, snapshotMock, loggerMock.Object);
        await source.FetchCatalogAsync();

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies that on the snapshot replay path, problem Content is correctly reconstructed
    /// from the raw detail response JSON stored in the capture, and all mapped DTO fields
    /// match the values in the raw catalog page JSON.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_ReplayPath_MapsFromRawJsonPreservingAllFields()
    {
        const string expectedContent = "<p>Given an array of integers...</p>";
        var rawCatalogPage = JsonSerializer.Serialize(new
        {
            data = new
            {
                problemsetQuestionList = new
                {
                    total = 1,
                    questions = new[]
                    {
                        new
                        {
                            titleSlug = "two-sum",
                            frontendQuestionId = "1",
                            title = "Two Sum",
                            difficulty = "Easy",
                            topicTags = new[] { new { name = "Array", slug = "array" } }
                        }
                    }
                }
            }
        });
        var rawDetailResponse = JsonSerializer.Serialize(new
        {
            data = new { question = new { content = expectedContent } }
        });

        var replayCapture = new LeetCodeRawCaptureDto
        {
            RawCatalogPages = [rawCatalogPage],
            RawDetailResponses = new Dictionary<string, string> { ["two-sum"] = rawDetailResponse },
            TotalCount = 1
            // MappedNodes is empty — simulates snapshot replay
        };

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayCapture);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(1);
        result[0].TitleSlug.Should().Be("two-sum");
        result[0].QuestionId.Should().Be(1);
        result[0].Title.Should().Be("Two Sum");
        result[0].Difficulty.Should().Be("Easy");
        result[0].TopicTags.Should().ContainSingle(t => t == "Array");
        result[0].Content.Should().Be(expectedContent);
    }

    /// <summary>
    /// Verifies that Content populated on raw nodes during the live fetch is preserved
    /// through the snapshot persistence and replay cycle via the raw JSON strings.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_ContentIsPreservedThroughSnapshotReplay()
    {
        const string expectedContent = "<p>Given an array of integers...</p>";

        var rawCatalogPage = JsonSerializer.Serialize(new
        {
            data = new
            {
                problemsetQuestionList = new
                {
                    total = 1,
                    questions = new[]
                    {
                        new
                        {
                            titleSlug = "two-sum",
                            frontendQuestionId = "1",
                            title = "Two Sum",
                            difficulty = "Easy",
                            topicTags = new[] { new { name = "Array", slug = "array" } }
                        }
                    }
                }
            }
        });
        var rawDetailResponse = JsonSerializer.Serialize(new
        {
            data = new { question = new { content = expectedContent } }
        });

        var replayCapture = new LeetCodeRawCaptureDto
        {
            RawCatalogPages = [rawCatalogPage],
            RawDetailResponses = new Dictionary<string, string> { ["two-sum"] = rawDetailResponse },
            TotalCount = 1
        };

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayCapture);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(1);
        result[0].TitleSlug.Should().Be("two-sum");
        result[0].Content.Should().Be(expectedContent);
    }
}
