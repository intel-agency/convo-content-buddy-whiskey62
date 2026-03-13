using ConvoContentBuddy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;

namespace ConvoContentBuddy.Tests.DataLayer;

/// <summary>
/// Tests that <see cref="AppDbContext"/> is configured with the correct model metadata.
/// </summary>
public class AppDbContextConfigurationTests
{
    /// <summary>
    /// Subclass that captures the mutable model before EF Core finalises it into a RuntimeModel.
    /// EF Core 8+ strips design-time-only annotations (e.g. Postgres extensions) from the
    /// RuntimeModel, so tests that need those must inspect the mutable model captured here.
    /// </summary>
    private sealed class InspectableAppDbContext : AppDbContext
    {
        public IMutableModel? CapturedMutableModel { get; private set; }

        public InspectableAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            CapturedMutableModel = modelBuilder.Model;
        }
    }

    private static DbContextOptions<AppDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost", o => o.UseVector())
            .Options;

    private static AppDbContext CreateContext() => new AppDbContext(BuildOptions());

    private static InspectableAppDbContext CreateInspectableContext()
    {
        // Disable service-provider caching so EF Core doesn't reuse a model built by a plain
        // AppDbContext instance; without this, OnModelCreating on InspectableAppDbContext is
        // skipped and CapturedMutableModel remains null.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost", o => o.UseVector())
            .EnableServiceProviderCaching(false)
            .Options;
        var ctx = new InspectableAppDbContext(options);
        // Accessing ctx.Model triggers OnModelCreating, which populates CapturedMutableModel.
        _ = ctx.Model;
        return ctx;
    }

    /// <summary>Verifies the default schema is <c>app</c>.</summary>
    [Fact]
    public void DefaultSchema_IsApp()
    {
        using var ctx = CreateContext();
        ctx.Model.GetDefaultSchema().Should().Be("app");
    }

    /// <summary>Verifies the Problems entity maps to the <c>problems</c> table.</summary>
    [Fact]
    public void Problems_TableName_IsSnakeCase()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Problem))!;
        entityType.GetTableName().Should().Be("problems");
    }

    /// <summary>Verifies the Tags entity maps to the <c>tags</c> table.</summary>
    [Fact]
    public void Tags_TableName_IsSnakeCase()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Tag))!;
        entityType.GetTableName().Should().Be("tags");
    }

    /// <summary>Verifies the ProblemTags entity maps to the <c>problem_tags</c> table.</summary>
    [Fact]
    public void ProblemTags_TableName_IsSnakeCase()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.ProblemTag))!;
        entityType.GetTableName().Should().Be("problem_tags");
    }

    /// <summary>Verifies the IngestionSnapshots entity maps to the <c>ingestion_snapshots</c> table.</summary>
    [Fact]
    public void IngestionSnapshots_TableName_IsSnakeCase()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.IngestionSnapshot))!;
        entityType.GetTableName().Should().Be("ingestion_snapshots");
    }

    /// <summary>Verifies the Embedding column type is <c>vector(1536)</c>.</summary>
    [Fact]
    public void Embedding_ColumnType_IsVector1536()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Problem))!;
        var property = entityType.FindProperty(nameof(ConvoContentBuddy.Data.Entities.Problem.Embedding))!;
        property.GetColumnType().Should().Be("vector(1536)");
    }

    /// <summary>Verifies the Payload column type is <c>jsonb</c>.</summary>
    [Fact]
    public void Payload_ColumnType_IsJsonb()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.IngestionSnapshot))!;
        var property = entityType.FindProperty(nameof(ConvoContentBuddy.Data.Entities.IngestionSnapshot.Payload))!;
        property.GetColumnType().Should().Be("jsonb");
    }

    /// <summary>Verifies the unique index on <c>Slug</c>.</summary>
    [Fact]
    public void Problem_SlugIndex_IsUnique()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Problem))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(ConvoContentBuddy.Data.Entities.Problem.Slug)));
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    /// <summary>Verifies the unique index on <c>QuestionId</c>.</summary>
    [Fact]
    public void Problem_QuestionIdIndex_IsUnique()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Problem))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(ConvoContentBuddy.Data.Entities.Problem.QuestionId)));
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    /// <summary>Verifies the unique index on <c>Tag.Name</c>.</summary>
    [Fact]
    public void Tag_NameIndex_IsUnique()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Tag))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(ConvoContentBuddy.Data.Entities.Tag.Name)));
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    /// <summary>Verifies the composite primary key on <see cref="ConvoContentBuddy.Data.Entities.ProblemTag"/> contains both FKs.</summary>
    [Fact]
    public void ProblemTag_CompositePrimaryKey_ContainsProblemIdAndTagId()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.ProblemTag))!;
        var pk = entityType.FindPrimaryKey()!;
        var keyPropertyNames = pk.Properties.Select(p => p.Name).ToList();
        keyPropertyNames.Should().Contain(nameof(ConvoContentBuddy.Data.Entities.ProblemTag.ProblemId));
        keyPropertyNames.Should().Contain(nameof(ConvoContentBuddy.Data.Entities.ProblemTag.TagId));
    }

    /// <summary>Verifies cascade delete on all <see cref="ConvoContentBuddy.Data.Entities.ProblemTag"/> foreign keys.</summary>
    [Fact]
    public void ProblemTag_ForeignKeys_HaveCascadeDelete()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.ProblemTag))!;
        foreach (var fk in entityType.GetForeignKeys())
        {
            fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        }
    }

    /// <summary>Verifies the <c>vector</c> Postgres extension is registered in the model.</summary>
    [Fact]
    public void Model_HasPostgresExtension_Vector()
    {
        using var ctx = CreateInspectableContext();
        ctx.CapturedMutableModel!.GetAnnotations()
            .Should().Contain(a => a.Name == "Npgsql:PostgresExtension:vector");
    }

    /// <summary>Verifies the <c>uuid-ossp</c> Postgres extension is registered in the model.</summary>
    [Fact]
    public void Model_HasPostgresExtension_UuidOssp()
    {
        using var ctx = CreateInspectableContext();
        ctx.CapturedMutableModel!.GetAnnotations()
            .Should().Contain(a => a.Name == "Npgsql:PostgresExtension:uuid-ossp");
    }

    /// <summary>
    /// Verifies a check constraint enforcing <c>Easy|Medium|Hard</c> is configured on the <c>problems</c> table.
    /// </summary>
    [Fact]
    public void Problem_Difficulty_HasCheckConstraint_EnforcingAllowedValues()
    {
        using var ctx = CreateContext();
        var designTimeModel = ctx.GetService<IDesignTimeModel>().Model;
        var entityType = designTimeModel.FindEntityType(typeof(ConvoContentBuddy.Data.Entities.Problem))!;
        var constraint = entityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "CK_problems_difficulty");
        constraint.Should().NotBeNull("a check constraint named CK_problems_difficulty must be configured on the problems table");
        constraint!.Sql.Should().Be("difficulty IN ('Easy', 'Medium', 'Hard')");
    }
}
