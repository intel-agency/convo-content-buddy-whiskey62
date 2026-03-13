using System.Reflection;
using ConvoContentBuddy.Data.Repositories;

namespace ConvoContentBuddy.Tests.DataLayer;

/// <summary>
/// Tests that repository interface contracts are defined correctly.
/// </summary>
public class RepositoryContractTests
{
    /// <summary>Asserts that <see cref="IProblemRepository"/> is an interface.</summary>
    [Fact]
    public void IProblemRepository_IsInterface()
    {
        typeof(IProblemRepository).IsInterface.Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="ITagRepository"/> is an interface.</summary>
    [Fact]
    public void ITagRepository_IsInterface()
    {
        typeof(ITagRepository).IsInterface.Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="ISnapshotRepository"/> is an interface.</summary>
    [Fact]
    public void ISnapshotRepository_IsInterface()
    {
        typeof(ISnapshotRepository).IsInterface.Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemRepository"/> declares <c>UpsertBySlugAsync</c> with the correct signature.</summary>
    [Fact]
    public void IProblemRepository_HasUpsertBySlugMethod()
    {
        var method = typeof(IProblemRepository).GetMethod("UpsertBySlugAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<ConvoContentBuddy.Data.Entities.Problem>));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemRepository"/> declares <c>GetUnembeddedForProfileAsync</c> with a cancellation token.</summary>
    [Fact]
    public void IProblemRepository_HasGetUnembeddedMethod()
    {
        var method = typeof(IProblemRepository).GetMethod("GetUnembeddedForProfileAsync");
        method.Should().NotBeNull();
        HasCancellationTokenParam(method!).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemRepository"/> declares <c>UpdateEmbeddingAsync</c> returning <see cref="Task"/>.</summary>
    [Fact]
    public void IProblemRepository_HasUpdateEmbeddingMethod()
    {
        var method = typeof(IProblemRepository).GetMethod("UpdateEmbeddingAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemRepository"/> declares <c>CountAsync</c> returning <c>Task&lt;int&gt;</c>.</summary>
    [Fact]
    public void IProblemRepository_HasCountMethod()
    {
        var method = typeof(IProblemRepository).GetMethod("CountAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<int>));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemRepository"/> declares <c>SearchByVectorAsync</c> with a cancellation token.</summary>
    [Fact]
    public void IProblemRepository_HasSearchByVectorMethod()
    {
        var method = typeof(IProblemRepository).GetMethod("SearchByVectorAsync");
        method.Should().NotBeNull();
        HasCancellationTokenParam(method!).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="ITagRepository"/> declares <c>UpsertTagsAsync</c> with a cancellation token.</summary>
    [Fact]
    public void ITagRepository_HasUpsertTagsMethod()
    {
        var method = typeof(ITagRepository).GetMethod("UpsertTagsAsync");
        method.Should().NotBeNull();
        HasCancellationTokenParam(method!).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="ITagRepository"/> declares <c>GetOrCreateByNameAsync</c> returning a <see cref="ConvoContentBuddy.Data.Entities.Tag"/>.</summary>
    [Fact]
    public void ITagRepository_HasGetOrCreateByNameMethod()
    {
        var method = typeof(ITagRepository).GetMethod("GetOrCreateByNameAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<ConvoContentBuddy.Data.Entities.Tag>));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="ISnapshotRepository"/> declares <c>PersistSnapshotAsync</c> returning <see cref="Task"/>.</summary>
    [Fact]
    public void ISnapshotRepository_HasPersistSnapshotMethod()
    {
        var method = typeof(ISnapshotRepository).GetMethod("PersistSnapshotAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>
    /// Asserts that <see cref="ISnapshotRepository"/> declares <c>LoadLatestAsync</c> with a
    /// <c>source</c> string parameter and a cancellation token, enforcing source-scoped lookup semantics.
    /// </summary>
    [Fact]
    public void ISnapshotRepository_HasLoadLatestMethod_WithSourceParameter()
    {
        var method = typeof(ISnapshotRepository).GetMethod("LoadLatestAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(string) && p.Name == "source",
            because: "LoadLatestAsync must be scoped to a specific source to avoid ambiguity across ingestion sources");
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="ISnapshotRepository"/> declares <c>MarkAsLatestAsync</c> returning <see cref="Task"/>.</summary>
    [Fact]
    public void ISnapshotRepository_HasMarkAsLatestMethod()
    {
        var method = typeof(ISnapshotRepository).GetMethod("MarkAsLatestAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemTagRepository"/> is an interface.</summary>
    [Fact]
    public void IProblemTagRepository_IsInterface()
    {
        typeof(IProblemTagRepository).IsInterface.Should().BeTrue();
    }

    /// <summary>Asserts that <see cref="IProblemTagRepository"/> declares <c>SyncTagsAsync</c> returning <see cref="Task"/>.</summary>
    [Fact]
    public void IProblemTagRepository_HasSyncTagsMethod()
    {
        var method = typeof(IProblemTagRepository).GetMethod("SyncTagsAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    /// <summary>
    /// Asserts that <see cref="IProblemTagRepository.SyncTagsAsync"/> accepts a <see cref="Guid"/> problem ID
    /// and an <see cref="IEnumerable{T}"/> of tag IDs.
    /// </summary>
    [Fact]
    public void IProblemTagRepository_SyncTagsMethod_HasExpectedParameters()
    {
        var method = typeof(IProblemTagRepository).GetMethod("SyncTagsAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(Guid));
        parameters.Should().Contain(p => p.ParameterType == typeof(IEnumerable<Guid>));
    }

    /// <summary>
    /// Asserts that <see cref="IProblemTagRepository"/> declares <c>GetTagsForProblemAsync</c>
    /// returning <c>Task&lt;IReadOnlyList&lt;Tag&gt;&gt;</c>.
    /// </summary>
    [Fact]
    public void IProblemTagRepository_HasGetTagsForProblemMethod()
    {
        var method = typeof(IProblemTagRepository).GetMethod("GetTagsForProblemAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<ConvoContentBuddy.Data.Entities.Tag>>));
        HasCancellationTokenParam(method).Should().BeTrue();
    }

    private static bool HasCancellationTokenParam(MethodInfo method) =>
        method.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken));
}
