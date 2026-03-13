using ConvoContentBuddy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConvoContentBuddy.Data;

/// <summary>
/// EF Core database context for ConvoContentBuddy.
/// </summary>
/// <remarks>
/// Consuming projects must call <c>.UseVector()</c> on the Npgsql options when registering
/// the database context to enable pgvector support. For example:
/// <code>
/// services.AddDbContext&lt;AppDbContext&gt;(o =>
///     o.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
/// </code>
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Gets or sets the Problems table.</summary>
    public DbSet<Problem> Problems { get; set; }

    /// <summary>Gets or sets the Tags table.</summary>
    public DbSet<Tag> Tags { get; set; }

    /// <summary>Gets or sets the ProblemTags join table.</summary>
    public DbSet<ProblemTag> ProblemTags { get; set; }

    /// <summary>Gets or sets the IngestionSnapshots table.</summary>
    public DbSet<IngestionSnapshot> IngestionSnapshots { get; set; }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasDefaultSchema("app");

        ConfigureProblem(modelBuilder);
        ConfigureTag(modelBuilder);
        ConfigureProblemTag(modelBuilder);
        ConfigureIngestionSnapshot(modelBuilder);
    }

    private static void ConfigureProblem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Problem>(entity =>
        {
            entity.ToTable("problems", t =>
                t.HasCheckConstraint("CK_problems_difficulty", "difficulty IN ('Easy', 'Medium', 'Hard')"));

            entity.Property(p => p.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("uuid_generate_v4()");

            entity.Property(p => p.Slug)
                .HasColumnName("slug")
                .IsRequired()
                .HasColumnType("text");

            entity.Property(p => p.QuestionId)
                .HasColumnName("question_id")
                .IsRequired();

            entity.Property(p => p.Title)
                .HasColumnName("title")
                .IsRequired()
                .HasColumnType("text");

            entity.Property(p => p.Difficulty)
                .HasColumnName("difficulty")
                .IsRequired()
                .HasColumnType("text");

            entity.Property(p => p.Description)
                .HasColumnName("description")
                .IsRequired()
                .HasColumnType("text");

            entity.Property(p => p.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(1536)");

            entity.Property(p => p.EmbeddingModel)
                .HasColumnName("embedding_model");

            entity.Property(p => p.EmbeddingDimensions)
                .HasColumnName("embedding_dimensions");

            entity.Property(p => p.EmbeddingGeneratedAt)
                .HasColumnName("embedding_generated_at");

            entity.Property(p => p.SeededAt)
                .HasColumnName("seeded_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.Property(p => p.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.HasIndex(p => p.Slug).IsUnique();
            entity.HasIndex(p => p.QuestionId).IsUnique();
            entity.HasIndex(p => p.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasFilter("embedding IS NOT NULL");
            entity.HasIndex(p => new { p.EmbeddingModel, p.EmbeddingDimensions });
        });
    }

    private static void ConfigureTag(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");

            entity.Property(t => t.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("uuid_generate_v4()");

            entity.Property(t => t.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasColumnType("text");

            entity.HasIndex(t => t.Name).IsUnique();
        });
    }

    private static void ConfigureProblemTag(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProblemTag>(entity =>
        {
            entity.ToTable("problem_tags");

            entity.HasKey(pt => new { pt.ProblemId, pt.TagId });

            entity.Property(pt => pt.ProblemId).HasColumnName("problem_id");
            entity.Property(pt => pt.TagId).HasColumnName("tag_id");

            entity.HasOne(pt => pt.Problem)
                .WithMany(p => p.ProblemTags)
                .HasForeignKey(pt => pt.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pt => pt.Tag)
                .WithMany(t => t.ProblemTags)
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIngestionSnapshot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngestionSnapshot>(entity =>
        {
            entity.ToTable("ingestion_snapshots");

            entity.Property(s => s.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("uuid_generate_v4()");

            entity.Property(s => s.Source)
                .HasColumnName("source")
                .IsRequired()
                .HasColumnType("text");

            entity.Property(s => s.CapturedAt)
                .HasColumnName("captured_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.Property(s => s.ProblemCount)
                .HasColumnName("problem_count")
                .IsRequired();

            entity.Property(s => s.Payload)
                .HasColumnName("payload")
                .IsRequired()
                .HasColumnType("jsonb");

            entity.Property(s => s.IsLatest)
                .HasColumnName("is_latest")
                .IsRequired()
                .HasDefaultValue(false);

            entity.HasIndex(s => s.CapturedAt).IsDescending(true);
        });
    }
}
