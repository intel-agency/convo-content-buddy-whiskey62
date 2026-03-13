using ConvoContentBuddy.Data.Seeder;
using ConvoContentBuddy.Data.Seeder.Models;
using ConvoContentBuddy.Data.Seeder.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConvoContentBuddy.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="ResilientLeetCodeDataSource"/> covering the live-first,
/// snapshot-fallback strategy including warning logging and exception propagation.
/// </summary>
public class ResilientLeetCodeDataSourceTests
{
    private static List<LeetCodeQuestionNodeDto> BuildNodes(int count = 2, bool withContent = false)
        => Enumerable.Range(1, count)
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

    private static ResilientLeetCodeDataSource CreateSource(
        Mock<ILeetCodeGraphQlClient> clientMock,
        Mock<ISnapshotService> snapshotMock,
        ILogger<ResilientLeetCodeDataSource>? logger = null)
        => new(
            clientMock.Object,
            snapshotMock.Object,
            logger ?? NullLogger<ResilientLeetCodeDataSource>.Instance);

    /// <summary>
    /// When the live client succeeds, the raw nodes are persisted as a snapshot and the
    /// mapped problems (with Content) are returned.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveSucceeds_PersistsSnapshotAndReturnsDtos()
    {
        var nodes = BuildNodes(5, withContent: true);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(nodes);

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.PersistSnapshotAsync(It.IsAny<IReadOnlyList<LeetCodeQuestionNodeDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(5);
        result[0].TitleSlug.Should().Be("slug-1");

        snapshotMock.Verify(
            s => s.PersistSnapshotAsync(
                It.Is<IReadOnlyList<LeetCodeQuestionNodeDto>>(p => p.Count == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// When the live client throws, the snapshot fallback returns the cached problems.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_LiveFails_FallsBackToSnapshot()
    {
        var cachedNodes = BuildNodes(3);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedNodes);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(3);
        result[0].TitleSlug.Should().Be("slug-1");
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
            .ReturnsAsync((IReadOnlyList<LeetCodeQuestionNodeDto>?)null);

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
        var cachedNodes = BuildNodes(1);

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedNodes);

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
    /// Verifies that Content populated on raw nodes during the live fetch is preserved
    /// through the snapshot persistence and replay cycle.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_ContentIsPreservedThroughSnapshotReplay()
    {
        const string expectedContent = "<p>Given an array of integers...</p>";
        var cachedNodes = new List<LeetCodeQuestionNodeDto>
        {
            new()
            {
                TitleSlug = "two-sum",
                QuestionFrontendId = "1",
                Title = "Two Sum",
                Difficulty = "Easy",
                TopicTags = [new LeetCodeTopicTagDto { Name = "Array", Slug = "array" }],
                Content = expectedContent
            }
        };

        var clientMock = new Mock<ILeetCodeGraphQlClient>();
        clientMock.Setup(c => c.FetchAllProblemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));

        var snapshotMock = new Mock<ISnapshotService>();
        snapshotMock.Setup(s => s.LoadLatestSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedNodes);

        var source = CreateSource(clientMock, snapshotMock);
        var result = await source.FetchCatalogAsync();

        result.Should().HaveCount(1);
        result[0].TitleSlug.Should().Be("two-sum");
        result[0].Content.Should().Be(expectedContent);
    }
}
